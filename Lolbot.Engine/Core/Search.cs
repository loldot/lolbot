using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using static System.Math;

namespace Lolbot.Core;

public sealed class Search(Game game, TranspositionTable tt, int[][] historyHeuristic)
{
    public const int Inf = short.MaxValue;
    public const int Mate = short.MaxValue / 2;

    const int Max_History = 38_400;
    const int Max_Depth = 64;

    private const int FutilityMargin = 130;
    private const int ReverseFutilityMargin = 117;

    private readonly MutablePosition rootPosition = game.CurrentPosition;
    private readonly RepetitionTable history = game.RepetitionTable;

    private readonly Move[] Killers = new Move[2 * Max_Depth];
    private int rootScore = -Inf;

    private int nodes = 0;
    private int qnodes = 0;
    private CancellationToken ct;

    NNUE.Accumulator[] accumulators = new NNUE.Accumulator[Max_Depth];
    public Action<SearchProgress>? OnSearchProgress { get; set; }
    public int CentiPawnEvaluation => rootScore;

    static readonly int[] LogTable = new int[256];
    static Search()
    {
        for (int i = 1; i < 256; i++)
        {
            LogTable[i] = (int)MathF.Round(128f * MathF.Log(i));
        }
    }

    public Move BestMove()
    {
        var timer = new CancellationTokenSource(2_000);
        return BestMove(timer.Token);
    }
    public Move BestMove(CancellationToken ct)
    {
        this.ct = ct;
        return IterativeDeepening(Max_Depth, ct);
    }

    public Move BestMove(int searchDepth)
    {
        this.ct = new CancellationTokenSource(60_000).Token;
        return IterativeDeepening(searchDepth, ct);
    }

    private Move[] rootMoves = new Move[256];
    private int[] rootMoveScores = new int[256];
    private int rootMoveCount = 0;

    public Move IterativeDeepening(int maxSearchDepth, CancellationToken ct)
    {
        this.ct = ct;
        Array.Clear(rootMoves);

        Span<Move> moves = rootMoves;
        rootMoveCount = MoveGenerator.Legal(rootPosition, ref moves);

        int offset = 0;

        if (tt.TryGet(rootPosition.Hash, out var ttEntry))
        {
            var ttMove = ttEntry.Move;
            int index;
            if (!ttMove.IsNull && (index = moves.IndexOf(ttMove)) >= 0)
            {
                (rootMoves[0], rootMoves[index]) = (rootMoves[index], rootMoves[0]);
                offset = 1;
            }
        }

        for (int i = offset; i < rootMoveCount; i++)
        {
            rootMoveScores[i] = -ScoreMove(rootMoves[i], 0);
        }
        Array.Sort(rootMoveScores, rootMoves, offset, rootMoveCount - offset);
        var depth = 1;

        while (depth <= maxSearchDepth && !ct.IsCancellationRequested)
        {
            AspirationWindows(depth);
            depth++;
        }

        return rootMoves[0];
    }

    public void AspirationWindows(int depth)
    {
        var delta = 64 / Clamp(depth - 3, 1, 4);

        var start = DateTime.Now;

        this.nodes = 0;
        this.qnodes = 0;

        var (alpha, beta) = depth <= 1
            ? (-Inf, Inf)
            : (rootScore - delta, rootScore + delta);

        while (true)
        {
            SearchRoot(depth, alpha, beta);

            if (rootScore <= alpha) alpha = rootScore - delta;
            else if (rootScore >= beta) beta = rootScore + delta;
            else break;

            delta <<= 1;
            if (delta >= 100) (alpha, beta) = (-(Inf + delta), Inf + delta);
        }

        var s = (DateTime.Now - start).TotalSeconds;
        OnSearchProgress?.Invoke(new SearchProgress(depth, rootMoves[0], rootScore, nodes, s));
        // Console.WriteLine($"DEBUG qnodes: {qnodes} ({qnodes * 100 / Max(nodes, 1)} %)");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static int Lmr(byte depth, byte move) => 1 + ((LogTable[depth] * LogTable[move + 1]) >> 15);


    public void SearchRoot(int depth, int alpha = -Inf, int beta = Inf)
    {
        if (rootPosition.IsCheck) depth++;

        rootScore = -Inf;
        var originalAlpha = alpha;
        Move bestMove = rootMoves[0];

        if (tt.TryGet(rootPosition.Hash, out var ttEntry))
        {
            if (ttEntry.Depth >= depth)
            {
                int ttEval = FromTT(ttEntry.Evaluation, 0);

                switch (ttEntry.Type)
                {
                    case TranspositionTable.Exact:
                        rootScore = ttEval;
                        return;
                    case TranspositionTable.LowerBound:
                        alpha = Max(alpha, ttEval);
                        break;
                    case TranspositionTable.UpperBound:
                        beta = Min(beta, ttEval);
                        break;
                }

                if (alpha >= beta)
                {
                    rootScore = ttEval;
                    return;
                }
            }
        }

        int i = 0;
        for (; i < rootMoveCount; i++)
        {
            var move = rootMoves[i];
            rootPosition.Move(in move);
            history.Update(move, rootPosition.Hash);

            int value;
            if (i == 0)
            {
                value = -EvaluateMove<PvNode>(rootPosition, depth - 1, 1, -beta, -alpha);
            }
            else
            {
                int reduction = Lmr((byte)depth, (byte)i);
                value = -EvaluateMove<NonPvNode>(rootPosition, depth - reduction, 1, -alpha - 1, -alpha);
                if (value > alpha && Abs(value) != Inf)
                    value = -EvaluateMove<PvNode>(rootPosition, depth - 1, 1, -beta, -alpha); // re-search
            }
            rootPosition.Undo(in move);
            history.Unwind();

            if (value > alpha && value < Inf)
            {
                if (i != 0) Array.Copy(rootMoves, 0, rootMoves, 1, rootMoveCount - 1);
                rootScore = alpha = value;
                rootMoves[0] = bestMove = move;
            }
        }
        nodes += i;

        var flag = TranspositionTable.Exact;
        if (alpha <= originalAlpha) flag = TranspositionTable.UpperBound;
        else if (alpha >= beta) flag = TranspositionTable.LowerBound;

        if (alpha > -Inf && alpha < Inf)
            tt.Add(rootPosition.Hash, depth, ToTT(alpha, 0), flag, bestMove);
    }

    public int EvaluateMove<TNode>(MutablePosition position, int depth, int ply, int alpha, int beta, bool isNullAllowed = true)
        where TNode : struct, NodeType
    {
        if (ct.IsCancellationRequested) return Inf;
        if (position.IsCheck) depth++;
        if (depth <= 0) return QuiesenceSearch<TNode>(position, alpha, beta);

        var originalAlpha = alpha;

        alpha = Max(alpha, -Mate + ply);
        beta = Min(beta, Mate - ply - 1);
        if (alpha >= beta) return alpha;

        if (history.IsDraw(position.Hash)) return 0;

        var ttMove = Move.Null;
        var eval = 0;

        ref var ttEntry = ref tt.GetRef((int)position.Hash);

        if (ttEntry.Key == position.Hash)
        {
            ttMove = ttEntry.Move;
            eval = FromTT(ttEntry.Evaluation, ply);

            if (ttEntry.Depth >= depth && (
                ttEntry.Type == TranspositionTable.Exact
                || (ttEntry.Type == TranspositionTable.LowerBound && eval >= beta)
                || (ttEntry.Type == TranspositionTable.UpperBound && eval <= alpha))
            ) return eval;
        }
        // else if (remainingDepth > 3) remainingDepth--;

        bool isPruningAllowed = !TNode.IsPv && !position.IsCheck;

        if (isPruningAllowed)
        {
            if (!ttEntry.IsSet || ttEntry.Type != TranspositionTable.Exact)
            {
                eval = position.Eval;
            }

            var margin = ReverseFutilityMargin * depth;

            // Reverse futility pruning
            if (depth <= 5 && eval - margin >= beta) return eval - margin;

            // Adaptive null move pruning
            if (eval >= beta - 21 * depth + 421 && isNullAllowed && !position.IsEndgame)
            {
                position.SkipTurn();
                var r = Clamp(depth * (eval - beta) / Heuristics.KnightValue, 1, 7);
                var score = -EvaluateMove<NonPvNode>(position, depth - r, ply + 1, -beta, -beta + 1, isNullAllowed: false);
                position.UndoSkipTurn();

                if (score >= beta)
                {
                    return eval;
                }
            }
        }
        var best = -Inf;
        byte i = 0;
        Span<Move> moves = stackalloc Move[256];
        var movepicker = new MovePicker(in Killers, ref historyHeuristic, ref moves, position, ttMove, ply);
        var move = movepicker.SelectMove(i);

        // No legal moves
        if (move.IsNull) return position.IsCheck ? -Mate + ply : 0;

        while (!move.IsNull)
        {
            position.Move(in move);
            history.Update(move, position.Hash);

            int score;
            if (TNode.IsPv && i == 0)
            {
                score = -EvaluateMove<PvNode>(position, depth - 1, ply + 1, -beta, -alpha);
            }
            else
            {
                int reduction = Lmr((byte)depth, i);
                score = -EvaluateMove<NonPvNode>(position, depth - reduction, ply + 1, -alpha - 1, -alpha);

                if (TNode.IsPv && score > alpha && score < beta)
                    score = -EvaluateMove<PvNode>(position, depth - 1, ply + 1, -beta, -alpha); // re-search
            }

            history.Unwind();
            position.Undo(in move);

            if (score > best)
            {
                best = score;
                ttMove = (ttMove.IsNull || best > originalAlpha) ? move : ttMove;
            } 

            if (score > alpha)
            {
                alpha = score;
                if (alpha >= beta)
                {
                    if (move.CapturePiece == Piece.None)
                    {
                        Killers[ply] = move;

                        var historyBonus = 300 * depth - 250;
                        UpdateHistory(move, historyBonus);

                        for (int q = 0; q < i; q++)
                        {
                            if (moves[q].CapturePiece == Piece.None)
                            {
                                UpdateHistory(moves[q], -historyBonus);
                            }
                        }
                    }
                    break;
                }
            }

            // Futility pruning
            if (isPruningAllowed && depth <= 3
                && move.CapturePiece == Piece.None
                && eval + FutilityMargin * depth <= alpha) break;

            move = movepicker.SelectMove(++i);
        }
        nodes += i;

        var flag = TranspositionTable.Exact;
        if (best <= originalAlpha) flag = TranspositionTable.UpperBound;
        else if (best >= beta) flag = TranspositionTable.LowerBound;

        if (best > -Inf && best < Inf)
            ttEntry = new TranspositionTable.Entry(position.Hash, depth, ToTT(best, ply), flag, ttMove);

        return best;
    }

    private int QuiesenceSearch<TNode>(MutablePosition position, int alpha, int beta) where TNode : struct, NodeType
    {
        int i = 0;
        Move move, ttMove = Move.Null;
        int[] deltas = [0, 180, 390, 442, 718, 1332, 88888]; // Piece values for delta pruning

        if (!TNode.IsPv && tt.TryGet(position.Hash, out var ttEntry))
        {
            ttMove = ttEntry.Move;
            if (ttEntry.Type == TranspositionTable.Exact
                || (ttEntry.Type == TranspositionTable.LowerBound && ttEntry.Evaluation >= beta)
                || (ttEntry.Type == TranspositionTable.UpperBound && ttEntry.Evaluation <= alpha)
            ) return ttEntry.Evaluation;
        }

        Span<Move> moves = stackalloc Move[256];

        // var standPat = Heuristics.StaticEvaluation(position);
        var standPat = position.Eval;

        if (standPat >= beta) return beta;
        if (alpha < standPat) alpha = standPat;

        var movepicker = new MovePicker(in Killers, ref historyHeuristic, ref moves, position, ttMove, 0);

        while ((move = movepicker.PickCapture(i++)) != Move.Null)
        {
            if (standPat + deltas[(byte)move.CapturePieceType] < alpha)
            {
                if (!TNode.IsPv) return alpha;
                else continue;
            }

            if (position.SEE(move) < -45)
                continue;

            qnodes++;

            position.Move(in move);
            int eval = -QuiesenceSearch<TNode>(position, -beta, -alpha);
            position.Undo(in move);

            if (eval >= beta) return beta;

            alpha = Max(alpha, eval);
        }
        nodes += i;

        return alpha;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int ToTT(int score, int ply)
    {
        // Shift mate scores so closer mates are preferred from deeper nodes
        if (score > Mate - Max_Depth) return score + ply;
        if (score < -Mate + Max_Depth) return score - ply;
        return score;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int FromTT(int score, int ply)
    {
        if (score > Mate - Max_Depth) return score - ply;
        if (score < -Mate + Max_Depth) return score + ply;
        return score;
    }
   
    private int ScoreMove(Move m, int ply)
    {
        int score = 0;

        score += 1_000_000 * Heuristics.GetPieceValue(m.PromotionPiece);
        score += 100_000 * Heuristics.MVV_LVA(m.CapturePiece, m.FromPiece);
        score += Killers[ply] == m ? 99_999 : 0;
        score += historyHeuristic[m.Color][m.value & 0xfffu];

        return score;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void UpdateHistory(Move m, int bonus)
    {
        var index = m.value & 0xfff;
        bonus = Clamp(bonus, -Max_History, Max_History);
        historyHeuristic[m.Color][index] += bonus - historyHeuristic[m.Color][index] * Abs(bonus) / Max_History;
    }
}

public record SearchProgress(int Depth, Move BestMove, int Eval, int Nodes, double Time)
{
}

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
