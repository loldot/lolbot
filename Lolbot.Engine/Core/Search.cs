using System.Diagnostics;
using static System.Math;

namespace Lolbot.Core;

public interface NodeType
{
    static abstract bool IsPv { get; }
}
public readonly struct PvNode : NodeType
{
    public static bool IsPv => true;
}
public readonly struct NonPvNode : NodeType
{
    public static bool IsPv => false;
}

public sealed class Search(Game game, TranspositionTable tt, int[] historyHeuristic)
{
    public const int Inf = short.MaxValue;
    public const int Mate = short.MaxValue / 2;
    const int Max_Depth = 64;

    private readonly Position rootPosition = game.CurrentPosition;
    private readonly RepetitionTable history = game.RepetitionTable;

    private Move[] Killers = new Move[Max_Depth + 32];
    private int depth = 0;
    private int nodes = 0;
    private CancellationToken ct;

    public Move? BestMove()
    {
        var timer = new CancellationTokenSource(2_000);

        return BestMove(timer.Token);
    }

    public Move? BestMove(CancellationToken ct)
    {
        this.ct = ct;
        var bestMove = Move.Null;
        var score = -Inf;

        depth = 1;
        while (bestMove.IsNull || depth <= Max_Depth && !ct.IsCancellationRequested)
        {
            nodes = 0;
            var start = DateTime.Now;

            var delta = 64;

            while (true)
            {
                var (alpha, beta) = (depth > 1)
                    ? (score - delta, score + delta)
                    : (-Inf, Inf);

                (bestMove, score) = SearchRoot(depth, bestMove, alpha, beta);
                delta <<= 1;
                
                if (score <= alpha) alpha = score - delta;
                else if (score >= beta) beta = score + delta;
                else break;

                Console.WriteLine($"DEBUG research cp {score} depth {depth}"
                    + $" nodes {nodes} alpha {alpha} beta {beta} delta {delta}");
            }

            var s = (DateTime.Now - start).TotalSeconds;
            var nps = (int)(nodes / s);
            Console.WriteLine($"info score cp {score} depth {depth} bm {bestMove} nodes {nodes} nps {nps}");

            depth++;
        }

        return bestMove;
    }

    public (Move, int) SearchRoot(int depth, Move currentBest, int alpha = -Inf, int beta = Inf)
    {
        if (rootPosition.IsCheck) depth++;

        Span<Move> moves = stackalloc Move[256];
        var count = MoveGenerator.Legal(in rootPosition, ref moves);
        moves = moves[..count];

        var bestMove = currentBest.IsNull ? moves[0] : currentBest;

        int i = 0;
        for (; i < count; i++)
        {

            var move = SelectMove(ref moves, currentBest, in i, 0);
            var nextPosition = rootPosition.Move(move);

            int value = -Inf;
            history.Update(move, nextPosition.Hash);
            if (i == 0)
            {
                value = -EvaluateMove<PvNode>(in nextPosition, depth - 1, 1, -beta, -alpha);
            }
            else
            {
                value = -EvaluateMove<NonPvNode>(in nextPosition, depth - 1, 1, -alpha - 1, -alpha);
                if (value > alpha)
                    value = -EvaluateMove<PvNode>(in nextPosition, depth - 1, 1, -beta, -alpha); // re-search
            }

            history.Unwind();

            if (value > alpha)
            {
                alpha = value;
                bestMove = move;
            }
            if (ct.IsCancellationRequested) break;
        }
        nodes += i;

        return (bestMove, alpha);
    }

    public int EvaluateMove<TNode>(ref readonly Position position, int remainingDepth, int ply, int alpha, int beta)
        where TNode : struct, NodeType
    {
        if (remainingDepth == 0) return QuiesenceSearch(in position, alpha, beta);

        var mateValue = Mate - ply;
        var originalAlpha = alpha;

        if (alpha > mateValue) alpha = -mateValue;
        if (beta > mateValue - 1) beta = mateValue - 1;
        if (history.IsRepeated(position.Hash)) return 0;

        Span<Move> moves = stackalloc Move[218];
        var count = MoveGenerator.Legal(in position, ref moves);
        moves = moves[..count];

        // Checkmate or stalemate
        if (count == 0) return position.IsCheck ? -mateValue : 0;

        var ttMove = Move.Null;
        if (tt.TryGet(position.Hash, out var ttEntry))
        {
            if (ttEntry.Depth >= remainingDepth)
            {
                if (ttEntry.Type == TranspositionTable.Exact)
                {
                    return ttEntry.Evaluation;
                }
                else if (ttEntry.Type == TranspositionTable.LowerBound)
                {
                    alpha = Max(alpha, ttEntry.Evaluation);
                }
                else if (ttEntry.Type == TranspositionTable.UpperBound)
                {
                    beta = Min(beta, ttEntry.Evaluation);
                }

                if (alpha >= beta) return ttEntry.Evaluation;
            }
            ttMove = ttEntry.Move;
        }
        // else if (remainingDepth > 3) remainingDepth--;

        var value = -Inf;

        int i = 0;
        for (; i < count; i++)
        {
            var move = SelectMove(ref moves, ttMove, in i, ply);
            var nextPosition = position.Move(move);

            history.Update(move, nextPosition.Hash);
            if (TNode.IsPv)
            {
                value = -EvaluateMove<TNode>(in nextPosition, remainingDepth - 1, ply + 1, -beta, -alpha);
            }
            else
            {
                value = -EvaluateMove<NonPvNode>(in nextPosition, remainingDepth - 1, ply + 1, -alpha - 1, -alpha);
                if (value > alpha && TNode.IsPv)
                    value = -EvaluateMove<PvNode>(in nextPosition, remainingDepth - 1, ply + 1, -beta, -alpha); // re-search
            }
            history.Unwind();

            value = Max(value, alpha);

            if (value > alpha)
            {
                alpha = value;
                ttMove = moves[i];
                if (alpha >= beta)
                {
                    if (moves[i].CapturePiece == Piece.None)
                    {
                        historyHeuristic[moves[i].FromIndex * 64 + moves[i].ToIndex] = remainingDepth * remainingDepth;
                        for (int q = 0; q < i; q++)
                        {
                            if (moves[q].CapturePiece == Piece.None)
                            {
                                historyHeuristic[moves[q].FromIndex * 64 + moves[q].ToIndex] -= remainingDepth * remainingDepth;
                            }
                        }
                        Killers[ply] = move;
                    }
                    break;
                }
            }
        }
        nodes += i;

        var flag = TranspositionTable.Exact;
        if (value <= originalAlpha) flag = TranspositionTable.UpperBound;
        else if (value >= beta) flag = TranspositionTable.LowerBound;

        if (value != 0)
            tt.Add(position.Hash, remainingDepth, value, flag, ttMove);

        return value;
    }

    private int QuiesenceSearch(in Position position, int alpha, int beta)
    {
        Span<Move> moves = stackalloc Move[218];

        var count = MoveGenerator.Captures(in position, ref moves);
        moves = moves[..count];

        var standPat = StaticEvaluation(in position);

        if (standPat >= beta) return beta;
        if (alpha < standPat) alpha = standPat;

        int i = 0;
        for (; i < count; i++)
        {
            var move = SelectMove(ref moves, Move.Null, i, 0);
            var nextPosition = position.Move(move);
            var eval = -QuiesenceSearch(in nextPosition, -beta, -alpha);

            if (eval >= beta) return beta;

            alpha = Max(eval, alpha);
        }
        nodes += i;

        return alpha;
    }

    private static readonly float[] GamePhaseInterpolation = [
        0,  0,  0,  0,  0,  0,  0,  0,
        0.04f, 0.08f, 0.16f, 0.20f, 0.24f, 0.28f, 0.32f, 0.36f,
        0.40f, 0.44f, 0.48f, 0.52f, 0.56f, 0.60f, 0.64f, 0.68f,
        0.72f, 0.76f, 0.80f, 0.84f, 0.88f, 0.92f, 0.96f, 1f, 1f
    ];

    public static int StaticEvaluation(ref readonly Position position)
    {
        var whitePawns = Bitboards.CountOccupied(position.WhitePawns);
        var whiteKnigts = Bitboards.CountOccupied(position.WhiteKnights);
        var whiteBishops = Bitboards.CountOccupied(position.WhiteBishops);
        var whiteRooks = Bitboards.CountOccupied(position.WhiteRooks);
        var whiteQueens = Bitboards.CountOccupied(position.WhiteQueens);
        
        var whitePieceMaterial 
            = whiteKnigts * Heuristics.KnightValue 
            + whiteBishops * Heuristics.BishopValue
            + whiteRooks * Heuristics.RookValue
            + whiteQueens * Heuristics.QueenValue;

        var blackPawns = Bitboards.CountOccupied(position.BlackPawns);
        var blackKnigts = Bitboards.CountOccupied(position.BlackKnights);
        var blackBishops = Bitboards.CountOccupied(position.BlackBishops);
        var blackRooks = Bitboards.CountOccupied(position.BlackRooks);
        var blackQueens = Bitboards.CountOccupied(position.BlackQueens);

        var blackPieceMaterial 
            = blackKnigts * Heuristics.KnightValue 
            + blackBishops * Heuristics.BishopValue
            + blackRooks * Heuristics.RookValue
            + blackQueens * Heuristics.QueenValue;

        var phase = (Heuristics.StartMaterialValue - whitePieceMaterial - blackPieceMaterial) 
            / Heuristics.StartMaterialValue;

        int eval = 0, middle = 0, end = 0;
        
        eval += whitePieceMaterial + (whitePawns * Heuristics.PawnValue);
        eval -= blackPieceMaterial + (blackPawns * Heuristics.PawnValue);

        for (Piece i = Piece.WhitePawn; i < Piece.WhiteKing; i++)
        {
            var (mgw, egw) = Heuristics.GetPieceValue(i, position[i]);

            middle += mgw;
            end += egw;
        }

        for (Piece i = Piece.BlackPawn; i < Piece.BlackKing; i++)
        {
            var (mgb, egb) = Heuristics.GetPieceValue(i, position[i]);
            middle -= mgb;
            end -= egb;
        }

        middle += Heuristics.KingSafety(in position, Colors.White);
        middle -= Heuristics.KingSafety(in position, Colors.Black);

        var color = position.CurrentPlayer == Colors.White ? 1 : -1;
        eval = (int)float.Lerp(eval + middle, eval + end, phase);

        if (position.IsCheck) eval -= color * 50;

        eval += Heuristics.PawnStructure(position.WhitePawns, position.BlackPawns, Colors.White);
        eval -= Heuristics.PawnStructure(position.BlackPawns, position.WhitePawns, Colors.Black);

        eval += Heuristics.Mobility(in position, Colors.White);
        eval -= Heuristics.Mobility(in position, Colors.Black);

        return color * eval;
    }

    private ref readonly Move SelectMove(ref Span<Move> moves, in Move currentBest, in int k, int ply)
    {
        if (k == 0 && !currentBest.IsNull)
        {
            var index = moves.IndexOf(currentBest);
            if (index >= 0)
            {
                moves[index] = moves[0];
                moves[0] = currentBest;
                return ref moves[0];
            }
        }

        var n = moves.Length;
        if (k <= 8)
        {
            int bestScore = 0;
            int bestIndex = k;
            for (var i = k; i < n; i++)
            {
                var score = ScoreMove(moves[i], ply);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestIndex = i;
                }
            }

            (moves[bestIndex], moves[k]) = (moves[k], moves[bestIndex]);
        }

        return ref moves[k];
    }

    private int MoveComparer(Move x, Move y)
    {
        int score = 0;

        score -= 1_000_000 * Heuristics.GetPieceValue(x.PromotionPiece);
        score -= 100_000 * Heuristics.MVV_LVA(x.CapturePiece, x.FromPiece);
        score -= historyHeuristic[64 * x.FromIndex + x.ToIndex];

        score += 1_000_000 * Heuristics.GetPieceValue(y.PromotionPiece);
        score += 100_000 * Heuristics.MVV_LVA(y.CapturePiece, y.FromPiece);
        score += historyHeuristic[64 * y.FromIndex + y.ToIndex];

        return score;
    }

    private int ScoreMove(Move m, int ply)
    {
        int score = 0;

        score += 1_000_000 * Heuristics.GetPieceValue(m.PromotionPiece);
        score += 100_000 * Heuristics.MVV_LVA(m.CapturePiece, m.FromPiece);
        score += Killers[ply] == m ? 99_999 : 0;
        score += historyHeuristic[64 * m.FromIndex + m.ToIndex];

        return score;
    }
}