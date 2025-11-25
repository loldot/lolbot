using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using static System.Math;

namespace Lolbot.Core;

public sealed class Search(Game game, TranspositionTable tt, int[][] historyHeuristic)
{
    private const int Max_Depth = 64;
    private readonly MutablePosition rootPosition = game.CurrentPosition;
    private readonly RepetitionTable history = game.RepetitionTable;
    private long nodes;
    private readonly Move[,] _killerMoves = new Move[Max_Depth, 2];

    private CancellationToken ct;

    public Action<SearchProgress>? OnSearchProgress { get; set; }
    public int CentiPawnEvaluation { get; private set; }

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
        nodes = 0;
        Move bestMove = Move.Null;
        var watch = Stopwatch.StartNew();

        for (int depth = 1; depth <= maxSearchDepth; depth++)
        {
            var score = AlphaBeta(rootPosition, -Evaluation.Infinity, Evaluation.Infinity, depth, 0);

            if (ct.IsCancellationRequested)
            {
                break;
            }

            // This is a fail-high/low, research
            if (score == -Evaluation.Infinity || score == Evaluation.Infinity)
            {
                score = AlphaBeta(rootPosition, -Evaluation.Infinity, Evaluation.Infinity, depth, 0);
            }

            var pv = tt.GetPv(rootPosition, depth);
            if (pv.Length > 0)
            {
                bestMove = pv[0];
            }

            CentiPawnEvaluation = score;
            OnSearchProgress?.Invoke(new SearchProgress(depth, bestMove, score, (int)nodes, watch.Elapsed.TotalMilliseconds));
        }

        return bestMove;
    }

    private int AlphaBeta(MutablePosition position, int alpha, int beta, int depth, int ply)
    {
        if (depth <= 0)
        {
            return QuiescenceSearch(position, alpha, beta);
        }

        if (ct.IsCancellationRequested)
        {
            return 0;
        }

        Move bestMove = Move.Null;
        int bestScore = -Evaluation.Infinity;
        byte ttType = TranspositionTable.UpperBound;

        if (tt.TryGet(position.Hash, depth, out var entry))
        {
            if (entry.Type == TranspositionTable.Exact)
                return entry.Evaluation;
            if (entry.Type == TranspositionTable.LowerBound)
                alpha = Max(alpha, entry.Evaluation);
            else if (entry.Type == TranspositionTable.UpperBound)
                beta = Min(beta, entry.Evaluation);

            if (alpha >= beta)
                return entry.Evaluation;
        }

        // Null Move Pruning
        if (depth >= 3 && !position.IsCheck && position.IsEndgame == false)
        {
            position.SkipTurn();
            int nullMoveScore = -AlphaBeta(position, -beta, -beta + 1, depth - 1 - 2, ply + 1);
            position.UndoSkipTurn();
            if (nullMoveScore >= beta)
            {
                return beta;
            }
        }

        var moves = position.GenerateLegalMoves();
        if (moves.Length == 0)
        {
            nodes++;
            return position.IsCheck ? -Evaluation.Checkmate + ply : 0;
        }

        // Move ordering
        OrderMoves(position, moves, entry.Move, ply);


        foreach (var move in moves)
        {
            position.Move(in move);
            var score = -AlphaBeta(position, -beta, -alpha, depth - 1, ply + 1);
            position.Undo(in move);

            if (score > bestScore)
            {
                bestScore = score;
                bestMove = move;
            }

            if (bestScore >= beta)
            {
                tt.Add(position.Hash, depth, bestScore, TranspositionTable.LowerBound, bestMove);
                if (move.CapturePieceType == PieceType.None)
                {
                    UpdateKillerMoves(ply, move);
                    historyHeuristic[(int)move.FromPieceType - 1][move.ToIndex] += depth * depth;
                }
                return bestScore; // Fail hard beta-cutoff
            }
            if (bestScore > alpha)
            {
                alpha = bestScore;
                ttType = TranspositionTable.Exact;
            }
        }

        tt.Add(position.Hash, depth, bestScore, ttType, bestMove);
        return bestScore;
    }

    private void OrderMoves(MutablePosition position, Span<Move> moves, Move ttMove, int ply)
    {
        Span<int> scores = stackalloc int[moves.Length];
        for (int i = 0; i < moves.Length; i++)
        {
            scores[i] = ScoreMove(moves[i], ttMove, ply);
        }

        // Simple insertion sort
        for (int i = 1; i < moves.Length; i++)
        {
            var move = moves[i];
            var score = scores[i];
            int j = i - 1;
            while (j >= 0 && scores[j] < score)
            {
                moves[j + 1] = moves[j];
                scores[j + 1] = scores[j];
                j--;
            }
            moves[j + 1] = move;
            scores[j + 1] = score;
        }
    }

    private int ScoreMove(Move move, Move ttMove, int ply)
    {
        if (move == ttMove) return 100_000;
        if (move.CapturePieceType != PieceType.None)
        {
            // MVV-LVA (Most Valuable Victim - Least Valuable Aggressor)
            return 90_000 + ((int)move.CapturePieceType * 10) - (int)move.FromPieceType;
        }
        if (move == _killerMoves[ply, 0]) return 80_000;
        if (move == _killerMoves[ply, 1]) return 70_000;

        return historyHeuristic[(int)move.FromPieceType - 1][move.ToIndex];
    }

    private void UpdateKillerMoves(int ply, Move move)
    {
        if (_killerMoves[ply, 0] != move)
        {
            _killerMoves[ply, 1] = _killerMoves[ply, 0];
            _killerMoves[ply, 0] = move;
        }
    }

    private int QuiescenceSearch(MutablePosition position, int alpha, int beta)
    {
        nodes++;
        if (ct.IsCancellationRequested)
        {
            return 0;
        }

        int standPat = position.Evaluate();
        if (standPat >= beta)
        {
            return beta;
        }
        if (alpha < standPat)
        {
            alpha = standPat;
        }

        var moves = position.GenerateLegalMoves().ToArray().Where(m => m.CapturePieceType != PieceType.None).ToArray();
        // Simple MVV-LVA sort for quiescence
        Array.Sort(moves, (a, b) => (((int)b.CapturePieceType * 10) - (int)b.FromPieceType).CompareTo(((int)a.CapturePieceType * 10) - (int)a.FromPieceType));

        foreach (var move in moves)
        {
            position.Move(in move);
            int score = -QuiescenceSearch(position, -beta, -alpha);
            position.Undo(in move);

            if (score >= beta)
            {
                return beta;
            }
            if (score > alpha)
            {
                alpha = score;
            }
        }

        return alpha;
    }

}

public record SearchProgress(int Depth, Move BestMove, int Eval, int Nodes, double Time)
{
}