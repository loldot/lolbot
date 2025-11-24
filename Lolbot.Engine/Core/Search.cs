using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using static System.Math;

namespace Lolbot.Core;

public sealed class Search(Game game, TranspositionTable tt, int[][] historyHeuristic)
{
    private const int Max_Depth = 64;
    private const int MateValue = 32_000;
    private const int MateThreshold = MateValue - 256;
    private const int MaxMoves = 218;
    public const int Mate = MateValue;
    private readonly MutablePosition rootPosition = game.CurrentPosition;
    private readonly RepetitionTable history = game.RepetitionTable;
    private readonly int[][] historyTable = historyHeuristic;
    private readonly TranspositionTable transpositionTable = tt;

    private readonly Move[,] killerMoves = new Move[Max_Depth, 2];
    private readonly Move[,] pvTable = new Move[Max_Depth + 2, Max_Depth + 2];
    private readonly int[] pvLength = new int[Max_Depth + 2];
    private readonly Move[] principalVariation = new Move[Max_Depth + 2];
    private readonly Stopwatch stopwatch = new();

    private static readonly int[] PieceValues =
    [
        0,   // None
        100, // Pawn
        320, // Knight
        330, // Bishop
        500, // Rook
        900, // Queen
        0    // King (handled via mate detection)
    ];


    private CancellationToken ct;
    private bool stopRequested;
    private int nodes;

    public Action<SearchProgress>? OnSearchProgress { get; set; }
    public int CentiPawnEvaluation { get; private set; }


    static Search()
    {

    }

    public Move BestMove()
    {
        var timer = new CancellationTokenSource(2_000);
        return BestMove(timer.Token);
    }
    public Move BestMove(CancellationToken ct)
    {
        this.ct = ct;
        return DoSearch(Max_Depth, ct);
    }

    public Move BestMove(int searchDepth)
    {
        this.ct = new CancellationTokenSource(60_000).Token;
        return DoSearch(searchDepth, ct);
    }

    public Move DoSearch(int maxSearchDepth, CancellationToken ct)
    {
        this.ct = ct;
        stopRequested = false;
        nodes = 0;
        CentiPawnEvaluation = 0;

        Array.Clear(killerMoves);
        Array.Clear(pvLength);

        stopwatch.Restart();

        Move bestMove = Move.Null;

        Span<Move> rootMoves = stackalloc Move[MaxMoves];
        var moveCount = MoveGenerator.Legal(rootPosition, ref rootMoves);
        if (moveCount == 0)
        {
            stopwatch.Stop();
            return Move.Null;
        }

        for (int depth = 1; depth <= Min(maxSearchDepth, Max_Depth - 1); depth++)
        {
            if (stopRequested) break;

            int alpha = -MateValue;
            int beta = MateValue;

            var score = EvaluateMove<RootNode>(rootPosition, depth, 0, alpha, beta);

            if (stopRequested)
            {
                break;
            }

            CentiPawnEvaluation = score;

            if (pvLength[0] > 0)
            {
                int localLength = pvLength[0];
                for (int i = 0; i < localLength; i++)
                {
                    principalVariation[i] = pvTable[0, i];
                }

                bestMove = principalVariation[0];
            }
            else
            {
                bestMove = rootMoves[0];
                principalVariation[0] = bestMove;
            }

            OnSearchProgress?.Invoke(
                new SearchProgress(
                    depth,
                    bestMove,
                    score,
                    nodes,
                    stopwatch.Elapsed.TotalSeconds
                )
            );

            if (Abs(score) >= MateThreshold)
            {
                stopRequested = true;
                break;
            }

            if (ct.IsCancellationRequested)
            {
                stopRequested = true;
            }
        }

        stopwatch.Stop();

        if (bestMove.IsNull)
        {
            bestMove = rootMoves[0];
        }

        return bestMove;
    }

    public int EvaluateMove<TNode>(MutablePosition position, int depth, int ply, int alpha, int beta)
        where TNode : struct
    {
        if (stopRequested || ct.IsCancellationRequested)
        {
            stopRequested = true;
            return 0;
        }

        nodes++;

        bool isRoot = typeof(TNode) == typeof(RootNode);
        bool isPvNode = isRoot || typeof(TNode) == typeof(PvNode);

        alpha = Max(alpha, -MateValue + ply);
        beta = Min(beta, MateValue - ply);
        if (alpha >= beta) return alpha;

        if (history.IsDraw(position.Hash))
        {
            pvLength[ply] = 0;
            return 0;
        }

        if (ply >= Max_Depth - 1)
        {
            pvLength[ply] = 0;
            return EvaluateStatic(position);
        }

        bool inCheck = position.IsCheck;
        if (inCheck)
        {
            depth++;
        }

        if (depth <= 0)
        {
            var qScore = Quiescence(position, ply, alpha, beta);
            pvLength[ply] = 0;
            return qScore;
        }

        int originalAlpha = alpha;
        Move ttMove = Move.Null;
        int ttEval = 0;

        if (transpositionTable.TryGet(position.Hash, out var entry))
        {
            ttMove = entry.Move;
            ttEval = UnpackMateScore(entry.Evaluation, ply);
            if (entry.Depth >= depth)
            {
                if (entry.Type == TranspositionTable.Exact)
                {
                    pvLength[ply] = 0;
                    return ttEval;
                }
                if (entry.Type == TranspositionTable.LowerBound)
                {
                    alpha = Max(alpha, ttEval);
                }
                else if (entry.Type == TranspositionTable.UpperBound)
                {
                    beta = Min(beta, ttEval);
                }

                if (alpha >= beta)
                {
                    pvLength[ply] = 0;
                    return ttEval;
                }
            }
        }

        Span<Move> moves = stackalloc Move[MaxMoves];
        int moveCount = MoveGenerator.Legal(position, ref moves);

        if (moveCount == 0)
        {
            pvLength[ply] = 0;
            if (inCheck)
            {
                return -MateValue + ply;
            }
            return 0;
        }

        Span<int> scores = stackalloc int[moveCount];
        var sideToMove = position.CurrentPlayer;

        for (int i = 0; i < moveCount; i++)
        {
            scores[i] = ScoreMove(in moves[i], ttMove, ply, sideToMove);
        }

        SortMoves(moves, scores, moveCount);

        int bestScore = -MateValue;
        Move bestMove = Move.Null;
        bool foundPv = false;

        pvLength[ply] = 0;

        for (int i = 0; i < moveCount; i++)
        {
            if (stopRequested) break;

            var move = moves[i];
            var moveCopy = move;
            position.Move(ref moveCopy);
            history.Update(moveCopy, position.Hash);

            int childScore;
            int childDepth = depth - 1;

            try
            {
                if (i == 0)
                {
                    childScore = -EvaluateMove<PvNode>(position, childDepth, ply + 1, -beta, -alpha);
                }
                else
                {
                    if (isPvNode)
                    {
                        childScore = -EvaluateMove<NonPvNode>(position, childDepth, ply + 1, -alpha - 1, -alpha);
                        if (childScore > alpha && childScore < beta)
                        {
                            childScore = -EvaluateMove<PvNode>(position, childDepth, ply + 1, -beta, -alpha);
                        }
                    }
                    else
                    {
                        childScore = -EvaluateMove<NonPvNode>(position, childDepth, ply + 1, -beta, -alpha);
                    }
                }
            }
            finally
            {
                history.Unwind();
                position.Undo(ref moveCopy);
            }

            if (stopRequested)
            {
                return 0;
            }

            if (childScore > bestScore)
            {
                bestScore = childScore;
                bestMove = move;
            }

            if (childScore > alpha)
            {
                alpha = childScore;
                foundPv = true;

                pvTable[ply, 0] = move;
                int childPvLength = pvLength[ply + 1];
                for (int j = 0; j < childPvLength; j++)
                {
                    pvTable[ply, j + 1] = pvTable[ply + 1, j];
                }
                pvLength[ply] = childPvLength + 1;

                if (alpha >= beta)
                {
                    if (move.IsQuiet)
                    {
                        StoreKiller(move, ply);
                        UpdateHistory(sideToMove, move, depth);
                    }
                    break;
                }
            }
            else if (i == 0)
            {
                pvLength[ply] = 0;
            }
        }

        if (stopRequested)
        {
            return 0;
        }

        byte boundType;
        if (!foundPv)
        {
            boundType = TranspositionTable.UpperBound;
            alpha = bestScore;
        }
        else if (alpha >= beta)
        {
            boundType = TranspositionTable.LowerBound;
        }
        else
        {
            boundType = TranspositionTable.Exact;
        }

        if (!stopRequested)
        {
            transpositionTable.Add(position.Hash, depth, PackMateScore(alpha, ply), boundType, bestMove);
        }

        return alpha;
    }

    private int Quiescence(MutablePosition position, int ply, int alpha, int beta)
    {
        if (stopRequested || ct.IsCancellationRequested)
        {
            stopRequested = true;
            return alpha;
        }

        nodes++;

        alpha = Max(alpha, -MateValue + ply);
        beta = Min(beta, MateValue - ply);
        if (alpha >= beta) return alpha;

        if (history.IsDraw(position.Hash))
        {
            return 0;
        }

        int standPat = EvaluateStatic(position);
        if (standPat >= beta) return standPat;
        if (standPat > alpha) alpha = standPat;

        Span<Move> moves = stackalloc Move[MaxMoves];
        int moveCount = MoveGenerator.Captures(position, ref moves);

        for (int i = 0; i < moveCount; i++)
        {
            if (stopRequested) break;

            var move = moves[i];
            var moveCopy = move;

            position.Move(ref moveCopy);
            history.Update(moveCopy, position.Hash);

            int score;
            try
            {
                score = -Quiescence(position, ply + 1, -beta, -alpha);
            }
            finally
            {
                history.Unwind();
                position.Undo(ref moveCopy);
            }

            if (stopRequested)
            {
                return alpha;
            }

            if (score >= beta)
            {
                return score;
            }
            if (score > alpha)
            {
                alpha = score;
            }
        }

        return alpha;
    }

    private int EvaluateStatic(MutablePosition position) => Heuristics.StaticEvaluation(position);

    private int ScoreMove(in Move move, Move ttMove, int ply, Colors sideToMove)
    {
        if (move == ttMove) return 1_000_000;

        if (move.CapturePieceType != PieceType.None)
        {
            return 500_000 + 16 * PieceValues[(int)move.CapturePieceType] - PieceValues[(int)move.FromPieceType];
        }

        if (killerMoves[ply, 0] == move) return 80_000;
        if (killerMoves[ply, 1] == move) return 70_000;

        int historyIndex = (move.FromIndex << 6) | move.ToIndex;
        int colorIndex = sideToMove == Colors.White ? 1 : 0;
        return historyTable[colorIndex][historyIndex];
    }

    private static void SortMoves(Span<Move> moves, Span<int> scores, int moveCount)
    {
        for (int i = 1; i < moveCount; i++)
        {
            var key = scores[i];
            var move = moves[i];
            int j = i - 1;
            while (j >= 0 && scores[j] < key)
            {
                scores[j + 1] = scores[j];
                moves[j + 1] = moves[j];
                j--;
            }
            scores[j + 1] = key;
            moves[j + 1] = move;
        }
    }

    private void StoreKiller(in Move move, int ply)
    {
        if (killerMoves[ply, 0] != move)
        {
            killerMoves[ply, 1] = killerMoves[ply, 0];
            killerMoves[ply, 0] = move;
        }
    }

    private void UpdateHistory(Colors sideToMove, in Move move, int depth)
    {
        if (!move.IsQuiet) return;

        int index = (move.FromIndex << 6) | move.ToIndex;
        int colorIndex = sideToMove == Colors.White ? 1 : 0;
        int bonus = depth * depth;

        ref int historyEntry = ref historyTable[colorIndex][index];
        historyEntry = Clamp(historyEntry + bonus, -32_768, 32_767);
    }

    private static short PackMateScore(int value, int ply)
    {
        if (value > MateThreshold)
        {
            value += ply;
        }
        else if (value < -MateThreshold)
        {
            value -= ply;
        }

        return (short)Clamp(value, short.MinValue + 1, short.MaxValue - 1);
    }

    private static int UnpackMateScore(short value, int ply)
    {
        int eval = value;
        if (eval > MateThreshold)
        {
            eval -= ply;
        }
        else if (eval < -MateThreshold)
        {
            eval += ply;
        }
        return eval;
    }
}

public readonly struct RootNode { }
public readonly struct PvNode { }
public readonly struct NonPvNode { }

public record SearchProgress(int Depth, Move BestMove, int Eval, int Nodes, double Time)
{
}