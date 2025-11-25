using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using static System.Math;

namespace Lolbot.Core;

public sealed class Search(Game game, TranspositionTable tt, int[][] historyHeuristic)
{
    private const int Max_Depth = 64;
    private const int Infinity = 999_999;
    private const int Mate = 20_000;
    private const int MateThreshold = Mate - 1000;
    
    private readonly MutablePosition rootPosition = game.CurrentPosition;
    private readonly RepetitionTable history = game.RepetitionTable;
    private readonly Move[][] killerMoves = new Move[Max_Depth][];
    private int nodesSearched = 0;
    private readonly Stopwatch searchTimer = new();
    
    private CancellationToken ct;

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
        
        // Initialize killer moves
        for (int i = 0; i < Max_Depth; i++)
        {
            killerMoves[i] = new Move[2];
        }
        
        nodesSearched = 0;
        searchTimer.Restart();
        
        Move bestMove = Move.Null;
        int bestScore = -Infinity;
        
        // Iterative deepening
        for (int depth = 1; depth <= maxSearchDepth; depth++)
        {
            if (ct.IsCancellationRequested)
                break;
                
            int alpha = -Infinity;
            int beta = Infinity;
            
            // Aspiration windows for depths > 4
            if (depth > 4)
            {
                int window = 50;
                alpha = Max(-Infinity, bestScore - window);
                beta = Min(Infinity, bestScore + window);
            }
            
            // Search with aspiration window
            int score = -Infinity;
            Move iterationBestMove = Move.Null;
            
            while (true)
            {
                score = AlphaBeta(depth, alpha, beta, 0, true);
                
                // Check if we need to re-search with wider window
                if (score <= alpha)
                {
                    alpha = -Infinity;
                    continue;
                }
                else if (score >= beta)
                {
                    beta = Infinity;
                    continue;
                }
                else
                {
                    break;
                }
            }
            
            // Get best move from transposition table
            if (tt.TryGet(rootPosition.Hash, out var entry) && !entry.Move.IsNull)
            {
                iterationBestMove = entry.Move;
            }
            
            if (!iterationBestMove.IsNull)
            {
                bestMove = iterationBestMove;
                bestScore = score;
                CentiPawnEvaluation = score;
            }
            
            // Report progress
            double timeSeconds = searchTimer.Elapsed.TotalSeconds;
            OnSearchProgress?.Invoke(new SearchProgress(
                depth,
                bestMove,
                score,
                nodesSearched,
                timeSeconds
            ));
            
            // Check for mate
            if (Abs(score) > MateThreshold)
                break;
        }
        
        searchTimer.Stop();
        
        return bestMove;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int AlphaBeta(int depth, int alpha, int beta, int ply, bool allowNull)
    {
        if (ct.IsCancellationRequested)
            return 0;
            
        bool isRoot = ply == 0;
        bool inCheck = rootPosition.IsCheck;
        
        // Check for draw by repetition
        if (!isRoot && history.IsDraw(rootPosition.Hash))
            return 0;
        
        // Probe transposition table
        if (tt.Probe(rootPosition.Hash, depth, ref alpha, ref beta, out Move ttMove, out int ttEval))
        {
            if (!isRoot)
                return ttEval;
        }
        
        // Quiescence search at leaf nodes
        if (depth <= 0)
            return Quiesce(alpha, beta, ply);
        
        nodesSearched++;
        
        // Null move pruning
        if (allowNull && !inCheck && depth >= 3 && !rootPosition.IsEndgame)
        {
            int R = depth > 6 ? 3 : 2;
            
            rootPosition.SkipTurn();
            int nullScore = -AlphaBeta(depth - 1 - R, -beta, -beta + 1, ply + 1, false);
            rootPosition.UndoSkipTurn();
            
            if (nullScore >= beta)
                return beta;
        }
        
        // Generate and order moves
        Span<Move> moves = stackalloc Move[218];
        int moveCount = MoveGenerator.Legal(rootPosition, ref moves);
        
        if (moveCount == 0)
        {
            return inCheck ? (-Mate + ply) : 0; // Checkmate or stalemate
        }
        
        // Order moves
        OrderMoves(moves[..moveCount], ttMove, ply);
        
        Move bestMove = Move.Null;
        int bestScore = -Infinity;
        byte nodeType = TranspositionTable.UpperBound;
        int movesSearched = 0;
        
        for (int i = 0; i < moveCount; i++)
        {
            ref readonly Move move = ref moves[i];
            
            rootPosition.Move(in move);
            int score;
            
            // Late move reductions (LMR)
            if (movesSearched >= 4 && depth >= 3 && !inCheck && move.IsQuiet && !rootPosition.IsCheck)
            {
                int reduction = 1;
                if (movesSearched >= 8) reduction = 2;
                if (depth >= 6 && movesSearched >= 12) reduction = 3;
                
                // Search with reduced depth
                score = -AlphaBeta(depth - 1 - reduction, -alpha - 1, -alpha, ply + 1, true);
                
                // Re-search if it raised alpha
                if (score > alpha)
                {
                    score = -AlphaBeta(depth - 1, -beta, -alpha, ply + 1, true);
                }
            }
            else
            {
                // Principal variation search (PVS)
                if (movesSearched == 0)
                {
                    score = -AlphaBeta(depth - 1, -beta, -alpha, ply + 1, true);
                }
                else
                {
                    // Null window search
                    score = -AlphaBeta(depth - 1, -alpha - 1, -alpha, ply + 1, true);
                    
                    // Re-search with full window if needed
                    if (score > alpha && score < beta)
                    {
                        score = -AlphaBeta(depth - 1, -beta, -alpha, ply + 1, true);
                    }
                }
            }
            
            rootPosition.Undo(in move);
            movesSearched++;
            
            if (score > bestScore)
            {
                bestScore = score;
                bestMove = move;
                
                if (score > alpha)
                {
                    alpha = score;
                    nodeType = TranspositionTable.Exact;
                    
                    // Update killers and history for quiet moves
                    if (move.IsQuiet)
                    {
                        UpdateKiller(move, ply);
                        UpdateHistory(move, depth);
                    }
                    
                    if (alpha >= beta)
                    {
                        nodeType = TranspositionTable.LowerBound;
                        break; // Beta cutoff
                    }
                }
            }
        }
        
        // Store in transposition table
        tt.Add(rootPosition.Hash, depth, bestScore, nodeType, bestMove);
        
        return bestScore;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int Quiesce(int alpha, int beta, int ply)
    {
        if (ct.IsCancellationRequested)
            return 0;
            
        nodesSearched++;
        
        // Stand pat evaluation
        int standPat = Evaluation.Evaluate(rootPosition);
        
        if (standPat >= beta)
            return beta;
            
        if (alpha < standPat)
            alpha = standPat;
        
        // Generate and search captures
        Span<Move> captures = stackalloc Move[218];
        int captureCount = MoveGenerator.Captures(rootPosition, ref captures);
        
        // Order captures by MVV-LVA
        OrderCaptures(captures[..captureCount]);
        
        for (int i = 0; i < captureCount; i++)
        {
            ref readonly Move capture = ref captures[i];
            
            // Delta pruning
            int captureValue = Evaluation.GetPieceValue(capture.CapturePieceType);
            if (standPat + captureValue + 200 < alpha)
                continue;
            
            rootPosition.Move(in capture);
            int score = -Quiesce(-beta, -alpha, ply + 1);
            rootPosition.Undo(in capture);
            
            if (score >= beta)
                return beta;
                
            if (score > alpha)
                alpha = score;
        }
        
        return alpha;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void OrderMoves(Span<Move> moves, Move ttMove, int ply)
    {
        Span<int> scores = stackalloc int[moves.Length];
        
        for (int i = 0; i < moves.Length; i++)
        {
            ref readonly Move move = ref moves[i];
            int score = 0;
            
            // TT move gets highest priority
            if (move == ttMove)
            {
                score = 1_000_000;
            }
            // Captures: MVV-LVA
            else if (move.CapturePieceType != PieceType.None)
            {
                int victimValue = Evaluation.GetPieceValue(move.CapturePieceType);
                int attackerValue = Evaluation.GetPieceValue(move.FromPieceType);
                score = 10_000 + victimValue * 10 - attackerValue;
            }
            // Promotions
            else if (move.PromotionPieceType != PieceType.None)
            {
                score = 9_000 + Evaluation.GetPieceValue(move.PromotionPieceType);
            }
            // Killer moves
            else if (ply < Max_Depth && (move == killerMoves[ply][0] || move == killerMoves[ply][1]))
            {
                score = 8_000;
            }
            // History heuristic
            else
            {
                score = historyHeuristic[move.FromIndex][move.ToIndex];
            }
            
            scores[i] = score;
        }
        
        // Simple selection sort for move ordering
        for (int i = 0; i < moves.Length - 1; i++)
        {
            int bestIdx = i;
            for (int j = i + 1; j < moves.Length; j++)
            {
                if (scores[j] > scores[bestIdx])
                    bestIdx = j;
            }
            
            if (bestIdx != i)
            {
                (moves[i], moves[bestIdx]) = (moves[bestIdx], moves[i]);
                (scores[i], scores[bestIdx]) = (scores[bestIdx], scores[i]);
            }
        }
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void OrderCaptures(Span<Move> captures)
    {
        Span<int> scores = stackalloc int[captures.Length];
        
        for (int i = 0; i < captures.Length; i++)
        {
            ref readonly Move capture = ref captures[i];
            int victimValue = Evaluation.GetPieceValue(capture.CapturePieceType);
            int attackerValue = Evaluation.GetPieceValue(capture.FromPieceType);
            scores[i] = victimValue * 10 - attackerValue;
        }
        
        // Simple selection sort
        for (int i = 0; i < captures.Length - 1; i++)
        {
            int bestIdx = i;
            for (int j = i + 1; j < captures.Length; j++)
            {
                if (scores[j] > scores[bestIdx])
                    bestIdx = j;
            }
            
            if (bestIdx != i)
            {
                (captures[i], captures[bestIdx]) = (captures[bestIdx], captures[i]);
                (scores[i], scores[bestIdx]) = (scores[bestIdx], scores[i]);
            }
        }
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void UpdateKiller(Move move, int ply)
    {
        if (ply >= Max_Depth) return;
        
        if (killerMoves[ply][0] != move)
        {
            killerMoves[ply][1] = killerMoves[ply][0];
            killerMoves[ply][0] = move;
        }
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void UpdateHistory(Move move, int depth)
    {
        historyHeuristic[move.FromIndex][move.ToIndex] += depth * depth;
    }
}

public record SearchProgress(int Depth, Move BestMove, int Eval, int Nodes, double Time)
{
}
