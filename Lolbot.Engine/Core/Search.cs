using System.Runtime.CompilerServices;
using static System.Math;

namespace Lolbot.Core;

public sealed class Search(Game game, TranspositionTable tt, int[] historyHeuristic)
{
    public const int Inf = short.MaxValue;
    public const int Mate = short.MaxValue / 2;

    const int Max_History = 38_400;
    const int Max_Depth = 128;
    const int Max_Extensions = 32;

    private readonly MutablePosition rootPosition = game.CurrentPosition;
    private readonly RepetitionTable history = game.RepetitionTable;

    private readonly Move[] Killers = new Move[2 * Max_Depth];
    private int rootScore = -Inf;

    private int nodes = 0;
    private int qnodes = 0;
    private CancellationToken ct;

    public Action<SearchProgress>? OnSearchProgress { get; set; }

    public Move? BestMove()
    {
        var timer = new CancellationTokenSource(2_000);

        return BestMove(timer.Token);
    }

    public Move? BestMove(CancellationToken ct)
    {
        this.ct = ct;
        var bestMove = Move.Null;

        var depth = 1;
        while (bestMove.IsNull || depth <= Max_Depth && !ct.IsCancellationRequested)
        {
            bestMove = BestMove(bestMove, depth);
            depth++;
        }

        return bestMove;
    }

    public Move BestMove(Move bestMove, int depth)
    {
        var delta = 64;

        var start = DateTime.Now;

        this.nodes = 0;
        this.qnodes = 0;

        var (alpha, beta) = depth <= 1
            ? (-Inf, Inf)
            : (rootScore - delta, rootScore + delta);

        while (true)
        {
            (bestMove, rootScore) = SearchRoot(depth, bestMove, alpha, beta);

            if (rootScore <= alpha) alpha = rootScore - delta;
            else if (rootScore >= beta) beta = rootScore + delta;
            else break;

            delta *= delta;

            Console.WriteLine($"DEBUG research cp {rootScore} depth {depth}"
                + $" nodes {nodes} alpha {alpha} beta {beta} delta {delta}");
        }

        var s = (DateTime.Now - start).TotalSeconds;
        OnSearchProgress?.Invoke(new SearchProgress(depth, bestMove, rootScore, nodes, s));
        Console.WriteLine($"DEBUG qnodes: {qnodes} ({qnodes * 100 / Max(nodes, 1)} %)");

        return bestMove;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static int Lmr(int depth, int move) => 1 + (int)(Log(depth) * Log(1 + move) / 2);

    public (Move, int) SearchRoot(int depth, Move currentBest, int alpha = -Inf, int beta = Inf)
    {
        if (rootPosition.IsCheck) depth++;

        var originalAlpha = alpha;

        var ttMove = Move.Null;
        if (tt.TryGet(rootPosition.Hash, out var ttEntry))
        {
            if (ttEntry.Depth >= depth)
            {
                if (ttEntry.Type == TranspositionTable.Exact)
                {
                    return (ttEntry.Move, ttEntry.Evaluation);
                }
                else if (ttEntry.Type == TranspositionTable.LowerBound)
                {
                    alpha = Max(alpha, ttEntry.Evaluation);
                }
                else if (ttEntry.Type == TranspositionTable.UpperBound)
                {
                    beta = Min(beta, ttEntry.Evaluation);
                }

                if (alpha >= beta)
                {
                    return (ttEntry.Move, ttEntry.Evaluation);
                }
            }
            ttMove = ttEntry.Move;
        }


        Span<Move> moves = stackalloc Move[256];
        var count = MoveGenerator.Legal(rootPosition, ref moves);
        moves = moves[..count];

        currentBest = currentBest.IsNull ? ttMove : currentBest;
        var bestMove = currentBest.IsNull ? moves[0] : currentBest;

        int i = 0;
        for (; i < count; i++)
        {

            var move = SelectMove(ref moves, currentBest, in i, 0);
            rootPosition.Move(in move);

            int value = -Inf;
            history.Update(move, rootPosition.Hash);
            if (i == 0)
            {
                value = -EvaluateMove<PvNode>(rootPosition, depth - 1, 1, -beta, -alpha);
            }
            else
            {
                int reduction = Lmr(depth, i);
                value = -EvaluateMove<NonPvNode>(rootPosition, depth - reduction, 1, -alpha - 1, -alpha);
                if (value > alpha)
                    value = -EvaluateMove<PvNode>(rootPosition, depth - 1, 1, -beta, -alpha); // re-search
            }
            rootPosition.Undo(in move);
            history.Unwind();

            if (value > alpha && value < Inf)
            {
                alpha = value;
                bestMove = move;
            }
        }
        nodes += i;

        var flag = TranspositionTable.Exact;
        if (alpha <= originalAlpha) flag = TranspositionTable.UpperBound;
        else if (alpha >= beta) flag = TranspositionTable.LowerBound;

        if (alpha != 0 && alpha > -Inf && alpha < Inf)
            tt.Add(rootPosition.Hash, depth, alpha, flag, bestMove);

        return (bestMove, alpha);
    }

    public int EvaluateMove<TNode>(MutablePosition position, int depth, int ply, int alpha, int beta, bool isNullAllowed = true)
        where TNode : struct, NodeType
    {
        if (ct.IsCancellationRequested) return Inf;
        if (position.IsCheck) depth++;
        if (depth <= 0) return QuiesenceSearch(position, alpha, beta);

        var mateValue = Mate - ply;
        var originalAlpha = alpha;

        if (alpha > mateValue) alpha = -mateValue;
        if (beta > mateValue - 1) beta = mateValue - 1;
        if (history.IsDraw(position.Hash)) return 0;

        // Checkmate or stalemate

        var ttMove = Move.Null;
        if (tt.TryGet(position.Hash, out var ttEntry))
        {
            if (ttEntry.Depth >= depth)
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

        if (!TNode.IsPv && !position.IsCheck)
        {
            var eval = Heuristics.StaticEvaluation(position);
            var margin = 117 * depth;

            if (depth <= 5 && eval - margin >= beta) return eval - margin;
            else if (eval >= beta - 21 * depth + 421 && isNullAllowed && !position.IsEndgame)
            {
                position.SkipTurn();
                var r = Clamp(depth * (eval - beta) / Heuristics.KnightValue, 1, 7);
                eval = -EvaluateMove<NonPvNode>(position, depth - r, ply + 1, -beta, -beta + 1, isNullAllowed: false);
                position.UndoSkipTurn();

                if (eval >= beta)
                {
                    return eval;
                }
            }
        }

        var value = -Inf;

        int i = 0;
        Span<Move> moves = stackalloc Move[256];
        var movepicker = new MovePicker(in Killers, ref historyHeuristic, ref moves, position, ttMove, ply);
        var move = movepicker.SelectMove(i);

        if (move.IsNull) return position.IsCheck ? -mateValue : 0;

        while (!move.IsNull)
        {
            position.Move(in move);
            history.Update(move, position.Hash);

            if (TNode.IsPv && i == 0)
            {
                value = -EvaluateMove<PvNode>(position, depth - 1, ply + 1, -beta, -alpha);
            }
            else
            {
                int reduction = Lmr(depth, i);
                value = -EvaluateMove<NonPvNode>(position, depth - reduction, ply + 1, -alpha - 1, -alpha);
                if (TNode.IsPv && value > alpha && value < beta)
                    value = -EvaluateMove<PvNode>(position, depth - 1, ply + 1, -beta, -alpha); // re-search
            }

            history.Unwind();
            position.Undo(in move);

            value = Max(value, alpha);

            if (value > alpha)
            {
                alpha = value;
                if (alpha > originalAlpha) ttMove = move;
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

            move = movepicker.SelectMove(++i);
        }
        nodes += i;

        var flag = TranspositionTable.Exact;
        if (value <= originalAlpha) flag = TranspositionTable.UpperBound;
        else if (value >= beta) flag = TranspositionTable.LowerBound;

        if (value != 0 && value > -Inf && value < Inf)
            tt.Add(position.Hash, depth, value, flag, ttMove);

        return value;
    }

    private int QuiesenceSearch(MutablePosition position, int alpha, int beta)
    {
        int i = 0;
        Move move;
        int[] deltas = { 0, 180, 390, 442, 718, 1332, 88888 }; // Piece values for delta pruning

        Span<Move> moves = stackalloc Move[256];

        var standPat = Heuristics.StaticEvaluation(position);

        if (standPat >= beta) return beta;
        if (alpha < standPat) alpha = standPat;

        var movepicker = new MovePicker(in Killers, ref historyHeuristic, ref moves, position, Move.Null, 0);

        while ((move = movepicker.PickCapture(i++)) != Move.Null)
        {
            if (standPat + deltas[(byte)move.CapturePieceType] < alpha)
                continue;

            // Static Exchange Evaluation (SEE): Skip bad captures
            if (position.SEE(move) < -45)
                continue;
            qnodes++;

            // Make the move
            position.Move(in move);
            int eval = -QuiesenceSearch(position, -beta, -alpha);
            position.Undo(in move);

            // Beta cutoff
            if (eval >= beta) return beta;

            // Update alpha
            alpha = Max(alpha, eval);
        }
        nodes += i;

        return alpha;
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
   
    private int ScoreMove(Move m, int ply)
    {
        int score = 0;

        score += 1_000_000 * Heuristics.GetPieceValue(m.PromotionPiece);
        score += 100_000 * Heuristics.MVV_LVA(m.CapturePiece, m.FromPiece);
        score += Killers[ply] == m ? 99_999 : 0;
        score += historyHeuristic[m.value & 0xfffu];

        return score;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void UpdateHistory(Move m, int bonus)
    {
        var index = m.value & 0xfff;
        bonus = Clamp(bonus, -Max_History, Max_History);
        historyHeuristic[index] += bonus - historyHeuristic[index] * Abs(bonus) / Max_History;
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