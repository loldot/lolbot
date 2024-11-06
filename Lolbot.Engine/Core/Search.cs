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

public sealed class Search(Game game, TranspositionTable tt)
{
    public const int Inf = short.MaxValue;
    public const int Mate = short.MaxValue / 2;
    const int Max_Depth = 64;

    private readonly Position rootPosition = game.CurrentPosition;
    private readonly RepetitionTable history = game.RepetitionTable;

    private int searchDepth = 0;
    private int nodes = 0;
    private CancellationToken ct;
    private bool isAborted;

    public Move? BestMove()
    {
        var timer = new CancellationTokenSource(2_000);

        return BestMove(timer.Token);
    }

    public Move? BestMove(CancellationToken ct)
    {
        this.ct = ct;
        var bestMove = default(Move?);
        searchDepth = 1;

        while (bestMove is null || searchDepth <= Max_Depth && !ct.IsCancellationRequested)
        {
            nodes = 0;

            var temp = SearchRoot(searchDepth, bestMove);
            if (!isAborted) bestMove = temp;

            searchDepth++;
        }

        return bestMove;
    }

    public Move SearchRoot(int depth, Move? currentBest)
    {
        if (rootPosition.IsCheck) depth++;

        Span<Move> moves = stackalloc Move[256];
        var count = MoveGenerator.Legal(in rootPosition, ref moves);
        moves = moves[..count];

        var bestMove = currentBest ?? moves[0];
        var start = DateTime.Now;

        var (alpha, beta) = (-Inf, Inf);

        int i = 0;
        for (; i < count; i++)
        {
            if (ct.IsCancellationRequested) break;

            var move = SelectMove(ref moves, currentBest, in i);
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
        }
        nodes += i;
        var ms = (DateTime.Now - start).TotalMilliseconds;
        var nps = (int)(nodes * 1000 / ms);
        Console.WriteLine($"info score cp {alpha} depth {depth} bm {bestMove} nodes {nodes} nps {nps}");

        return bestMove;
    }

    public int EvaluateMove<TNode>(ref readonly Position position, int remainingDepth, int ply, int alpha, int beta)
        where TNode : struct, NodeType
    {
        if (remainingDepth == 0) return QuiesenceSearch(in position, alpha, beta);

        var mateValue = Mate - ply;
        var originalAlpha = alpha;

        if (alpha > mateValue) alpha = -mateValue;
        if (beta > mateValue - 1) beta = mateValue - 1;
        if (history.IsDrawByRepetition(position.Hash)) return 0;

        Span<Move> moves = stackalloc Move[218];
        var count = MoveGenerator.Legal(in position, ref moves);
        moves = moves[..count];

        // Checkmate or stalemate
        if (count == 0) return position.IsCheck ? -mateValue : 0;

        if ((nodes & 0xf) == 0 && ct.IsCancellationRequested)
        {
            isAborted = true;
            return QuiesenceSearch(in position, alpha, beta);
        }

        var ttMove = default(Move?);
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

        var value = -Inf;

        int i = 0;
        for (; i < count; i++)
        {
            var move = SelectMove(ref moves, ttMove, in i);
            var nextPosition = position.Move(move);

            if (i == 0)
            {
                value = -EvaluateMove<TNode>(in nextPosition, remainingDepth - 1, ply + 1, -beta, -alpha);
            }
            else
            {
                value = -EvaluateMove<NonPvNode>(in nextPosition, remainingDepth - 1, ply + 1, -alpha - 1, -alpha);
                if (value > alpha && TNode.IsPv)
                    value = -EvaluateMove<PvNode>(in nextPosition, remainingDepth - 1, ply + 1, -beta, -alpha); // re-search
            }
            value = Max(value, alpha);

            if (value > alpha)
            {
                alpha = value;
                ttMove = moves[i];
                if (alpha >= beta)
                {
                    break;
                }
            }
        }
        nodes += i;

        var flag = TranspositionTable.Exact;
        if (value <= originalAlpha) flag = TranspositionTable.UpperBound;
        else if (value >= beta) flag = TranspositionTable.LowerBound;

        tt.Add(position.Hash, remainingDepth, value, flag, ttMove ?? Move.Null);

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
            var move = SelectMove(ref moves, null, i);
            var nextPosition = position.Move(move);
            var eval = -QuiesenceSearch(in nextPosition, -beta, -alpha);

            if (eval >= beta) return beta;

            alpha = Max(eval, alpha);
        }
        nodes += i;

        return alpha;
    }

    public static int StaticEvaluation(ref readonly Position position)
    {
        var eval = 0;

        for (Piece i = Piece.WhitePawn; i < Piece.WhiteKing; i++)
        {
            eval += Heuristics.GetPieceValue(i, position[i], position.Occupied);
        }

        for (Piece i = Piece.BlackPawn; i < Piece.BlackKing; i++)
        {
            eval -= Heuristics.GetPieceValue(i, position[i], position.Occupied);
        }

        return position.CurrentPlayer == Color.White ? eval : -eval;
    }

    private static ref readonly Move SelectMove(ref Span<Move> moves, Move? currentBest, in int k)
    {
        if (k == 0 && currentBest is not null)
        {
            var index = moves.IndexOf(currentBest.Value);
            if (index >= 0)
            {
                moves[index] = moves[0];
                moves[0] = currentBest.Value;
            }
        }

        var n = moves.Length;
        if (k <= 8)
        {
            int bestIndex = k;
            for (var i = k; i < n; i++)
            {
                if (MoveComparer(moves[i], moves[bestIndex]) < 0) bestIndex = i;
            }

            (moves[bestIndex], moves[k]) = (moves[k], moves[bestIndex]);
        }

        return ref moves[k];
    }

    private static int MoveComparer(Move x, Move y)
    {
        int score = 0;

        score -= Heuristics.GetPieceValue(x.PromotionPiece);
        score -= Heuristics.MVV_LVA(x.CapturePiece, x.FromPiece);

        score += Heuristics.GetPieceValue(y.PromotionPiece);
        score += Heuristics.MVV_LVA(y.CapturePiece, y.FromPiece);

        return score;
    }
}