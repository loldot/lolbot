using System.Runtime.CompilerServices;
using static System.Math;

namespace Lolbot.Core;

public sealed class Search(Game game, TranspositionTable tt, int[][] historyHeuristic)
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
    static readonly int[] LogTable = new int[256];
    static Search()
    {
        for (int i = 1; i < 256; i++)
        {
            LogTable[i] = (int)MathF.Round(128f * MathF.Log(i));
        }
    }

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

    public Move? BestMove(int searchDepth)
    {
        this.ct = new CancellationTokenSource(60_000).Token;
        var bestMove = Move.Null;

        var depth = 1;
        while (bestMove.IsNull || depth <= searchDepth && !ct.IsCancellationRequested)
        {
            bestMove = BestMove(bestMove, depth);
            depth++;
        }

        return bestMove;
    }

    public Move BestMove(Move bestMove, int depth)
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
            (bestMove, rootScore) = SearchRoot(depth, bestMove, alpha, beta);

            if (rootScore <= alpha) alpha = rootScore - delta;
            else if (rootScore >= beta) beta = rootScore + delta;
            else break;

            delta <<= 1;
            if (delta >= 100) (alpha, beta) = (-(Inf + delta), Inf + delta);

            Console.WriteLine($"DEBUG research cp {rootScore} depth {depth}"
                + $" nodes {nodes} alpha {alpha} beta {beta} delta {delta}");
        }

        var s = (DateTime.Now - start).TotalSeconds;
        OnSearchProgress?.Invoke(new SearchProgress(depth, bestMove, rootScore, nodes, s));
        Console.WriteLine($"DEBUG qnodes: {qnodes} ({qnodes * 100 / Max(nodes, 1)} %)");

        return bestMove;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static int Lmr(byte depth, byte move) => 1 + ((LogTable[depth] * LogTable[move + 1]) >> 15);

    public (Move, int) SearchRoot(int depth, Move currentBest, int alpha = -Inf, int beta = Inf)
    {
        if (rootPosition.IsCheck) depth++;

        var originalAlpha = alpha;

        var ttMove = Move.Null;
        if (tt.TryGet(rootPosition.Hash, out var ttEntry))
        {
            if (ttEntry.Depth >= depth)
            {
                int ttEval = FromTT(ttEntry.Evaluation, 0);

                if (ttEntry.Type == TranspositionTable.Exact)
                {
                    return (ttEntry.Move, ttEval);
                }
                else if (ttEntry.Type == TranspositionTable.LowerBound)
                {
                    alpha = Max(alpha, ttEval);
                }
                else if (ttEntry.Type == TranspositionTable.UpperBound)
                {
                    beta = Min(beta, ttEval);
                }

                if (alpha >= beta)
                {
                    return (ttEntry.Move, ttEval);
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
                int reduction = Lmr((byte)depth, (byte)i);
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

        if (alpha > -Inf && alpha < Inf)
            tt.Add(rootPosition.Hash, depth, ToTT(alpha, 0), flag, bestMove);

        return (bestMove, alpha);
    }

    public int EvaluateMove<TNode>(MutablePosition position, int depth, int ply, int alpha, int beta, bool isNullAllowed = true)
        where TNode : struct, NodeType
    {
        if (ct.IsCancellationRequested) return Inf;
        if (position.IsCheck) depth++;
        if (depth <= 0) return QuiesenceSearch(position, alpha, beta);

        var originalAlpha = alpha;

        alpha = Max(alpha, -Mate + ply);
        beta = Min(beta, Mate - ply - 1);
        if (alpha >= beta) return alpha;

        if (history.IsDraw(position.Hash)) return 0;

        // TT probe (with mate normalization)
        var ttMove = Move.Null;
        var ttEval = 0;
        if (tt.TryGet(position.Hash, out var ttEntry))
        {
            if (ttEntry.Depth >= depth)
            {
                ttEval = FromTT(ttEntry.Evaluation, ply);

                if (ttEntry.Type == TranspositionTable.Exact)
                {
                    return ttEval;
                }
                else if (ttEntry.Type == TranspositionTable.LowerBound)
                {
                    alpha = Max(alpha, ttEval);
                }
                else if (ttEntry.Type == TranspositionTable.UpperBound)
                {
                    beta = Min(beta, ttEval);
                }

                if (alpha >= beta) return ttEval;
            }
            ttMove = ttEntry.Move;
        }
        // else if (remainingDepth > 3) remainingDepth--;

        if (!TNode.IsPv && !position.IsCheck)
        {
            int eval = ttEntry.IsSet && ttEntry.Type == TranspositionTable.Exact 
                ? ttEntry.Evaluation 
                : Heuristics.StaticEvaluation(position);
            // ttEntry switch
            // {
            //     { IsSet: true, Type: TranspositionTable.Exact } => ttEval,
            //     { IsSet: true, Type: TranspositionTable.LowerBound } => Max(Heuristics.StaticEvaluation(position), ttEval),
            //     { IsSet: true, Type: TranspositionTable.UpperBound } => Min(Heuristics.StaticEvaluation(position), ttEval),
            //     _ => 
            // };

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

            if (score > best) best = score;

            if (score > alpha)
            {
                alpha = score;
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
        if (best <= originalAlpha) flag = TranspositionTable.UpperBound;
        else if (best >= beta) flag = TranspositionTable.LowerBound;

        if (best > -Inf && best < Inf)
            tt.Add(position.Hash, depth, ToTT(best, ply), flag, ttMove);

        return best;
    }

    private int QuiesenceSearch(MutablePosition position, int alpha, int beta)
    {
        int i = 0;
        Move move;
        int[] deltas = [0, 180, 390, 442, 718, 1332, 88888]; // Piece values for delta pruning

        Span<Move> moves = stackalloc Move[256];

        var standPat = Heuristics.StaticEvaluation(position);

        if (standPat >= beta && !position.IsCheck) return beta;
        if (alpha < standPat) alpha = standPat;

        var movepicker = new MovePicker(in Killers, ref historyHeuristic, ref moves, position, Move.Null, 0);

        while (true)
        {
            move = position.IsCheck switch
            {
                true => movepicker.PickEvasion(i++),
                false => movepicker.PickCapture(i++)
            };
            if (move.IsNull) break;

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
