using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using static System.Math;

namespace Lolbot.Core;

public sealed class Search(Game game, TranspositionTable tt, int[][] historyHeuristic)
{
    private const int Max_Depth = 64;
    private const int Max_Quiescence_Depth = 16;
    
    private readonly MutablePosition rootPosition = game.CurrentPosition;
    private readonly RepetitionTable history = game.RepetitionTable;
    private readonly MoveOrdering moveOrdering = new(historyHeuristic);

    private CancellationToken ct;
    private Stopwatch searchTimer = new();
    private int nodesSearched = 0;
    
    // Principal Variation
    private readonly Move[][] pvTable = new Move[Max_Depth][];

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
        nodesSearched = 0;
        searchTimer.Restart();
        
        // Initialize PV table
        for (int i = 0; i < Max_Depth; i++)
        {
            pvTable[i] = new Move[Max_Depth];
        }

        Move bestMove = Move.Null;
        int alpha = -Evaluation.Mate;
        int beta = Evaluation.Mate;
        int bestScore = -Evaluation.Mate;

        // Iterative deepening
        for (int depth = 1; depth <= maxSearchDepth; depth++)
        {
            if (ct.IsCancellationRequested) break;

            int score = NegamaxRoot(depth, alpha, beta);

            // Check if search was cancelled during this iteration
            if (ct.IsCancellationRequested) break;

            bestScore = score;
            
            // Get best move from PV
            if (pvTable[0][0] != Move.Null)
            {
                bestMove = pvTable[0][0];
            }

            CentiPawnEvaluation = bestScore;

            // Report progress
            if (OnSearchProgress != null)
            {
                double elapsedSec = searchTimer.Elapsed.TotalSeconds;
                OnSearchProgress(new SearchProgress(
                    depth,
                    bestMove,
                    bestScore,
                    nodesSearched,
                    elapsedSec
                ));
            }

            // Aspiration window for next iteration
            if (depth >= 4 && !Evaluation.IsMateScore(bestScore))
            {
                const int aspirationWindow = 50;
                alpha = bestScore - aspirationWindow;
                beta = bestScore + aspirationWindow;
            }

            // If we found a mate, no need to search deeper
            if (Evaluation.IsMateScore(bestScore))
            {
                break;
            }
        }

        return bestMove;
    }

    private int NegamaxRoot(int depth, int alpha, int beta)
    {
        Span<Move> moves = stackalloc Move[218];
        int moveCount = MoveGenerator.Legal(rootPosition, ref moves);

        if (moveCount == 0)
        {
            return rootPosition.IsCheck ? Evaluation.MatedIn(0) : 0;
        }

        // Get TT move if available
        Move ttMove = Move.Null;
        if (tt.TryGet(rootPosition.Hash, out var entry))
        {
            ttMove = entry.Move;
        }

        // Order moves
        moveOrdering.OrderMoves(moves, moveCount, ttMove, 0);

        int bestScore = -Evaluation.Mate;
        Move bestMove = Move.Null;
        int originalAlpha = alpha;

        for (int i = 0; i < moveCount; i++)
        {
            if (ct.IsCancellationRequested) break;

            var move = moves[i];
            rootPosition.Move(ref move);
            nodesSearched++;

            int score;
            if (i == 0)
            {
                // Search first move with full window
                score = -Negamax(depth - 1, 1, -beta, -alpha);
            }
            else
            {
                // PVS: search with null window
                score = -Negamax(depth - 1, 1, -alpha - 1, -alpha);
                
                // Re-search if it failed high
                if (score > alpha && score < beta)
                {
                    score = -Negamax(depth - 1, 1, -beta, -alpha);
                }
            }

            rootPosition.Undo(ref move);

            if (score > bestScore)
            {
                bestScore = score;
                bestMove = move;

                // Update PV
                pvTable[0][0] = move;
                for (int j = 0; j < depth - 1; j++)
                {
                    pvTable[0][j + 1] = pvTable[1][j];
                }

                if (score > alpha)
                {
                    alpha = score;
                }
            }

            if (alpha >= beta)
            {
                break; // Beta cutoff
            }
        }

        // Store in transposition table
        byte nodeType = bestScore <= originalAlpha ? TranspositionTable.UpperBound :
                       bestScore >= beta ? TranspositionTable.LowerBound :
                       TranspositionTable.Exact;
        
        tt.Add(rootPosition.Hash, depth, bestScore, nodeType, bestMove);

        return bestScore;
    }

    private int Negamax(int depth, int ply, int alpha, int beta)
    {
        if (ct.IsCancellationRequested) return 0;

        // Check for draw by repetition
        if (ply > 0 && history.IsDraw(rootPosition.Hash))
        {
            return 0;
        }

        bool inCheck = rootPosition.IsCheck;

        // Check extension
        if (inCheck && depth < Max_Depth - 1)
        {
            depth++;
        }

        // Quiescence search at leaf nodes
        if (depth <= 0)
        {
            return Quiescence(ply, alpha, beta, 0);
        }

        // Probe transposition table
        Move ttMove = Move.Null;
        if (tt.Probe(rootPosition.Hash, depth, ref alpha, ref beta, out ttMove, out int ttScore))
        {
            return ttScore;
        }

        // Null move pruning
        if (!inCheck && depth >= 3 && ply > 0 && !rootPosition.IsEndgame)
        {
            const int R = 2; // Reduction factor
            
            rootPosition.SkipTurn();
            int nullScore = -Negamax(depth - 1 - R, ply + 1, -beta, -beta + 1);
            rootPosition.UndoSkipTurn();

            if (nullScore >= beta)
            {
                return beta;
            }
        }

        Span<Move> moves = stackalloc Move[218];
        int moveCount = MoveGenerator.Legal(rootPosition, ref moves);

        // Checkmate or stalemate
        if (moveCount == 0)
        {
            return inCheck ? Evaluation.MatedIn(ply) : 0;
        }

        // Order moves
        moveOrdering.OrderMoves(moves, moveCount, ttMove, ply);

        int bestScore = -Evaluation.Mate;
        Move bestMove = Move.Null;
        int originalAlpha = alpha;
        int movesSearched = 0;

        for (int i = 0; i < moveCount; i++)
        {
            var move = moves[i];
            
            rootPosition.Move(ref move);
            nodesSearched++;
            movesSearched++;

            int score;
            
            // Late Move Reductions (LMR)
            bool doLMR = movesSearched > 3 && depth >= 3 && !inCheck && move.IsQuiet && !rootPosition.IsCheck;
            
            if (doLMR)
            {
                int reduction = 1 + (movesSearched > 6 ? 1 : 0);
                score = -Negamax(depth - 1 - reduction, ply + 1, -alpha - 1, -alpha);
                
                // Re-search if LMR failed high
                if (score > alpha)
                {
                    score = -Negamax(depth - 1, ply + 1, -alpha - 1, -alpha);
                }
            }
            else if (i == 0)
            {
                // Search first move with full window
                score = -Negamax(depth - 1, ply + 1, -beta, -alpha);
            }
            else
            {
                // PVS: search with null window
                score = -Negamax(depth - 1, ply + 1, -alpha - 1, -alpha);
                
                // Re-search if it failed high
                if (score > alpha && score < beta)
                {
                    score = -Negamax(depth - 1, ply + 1, -beta, -alpha);
                }
            }

            rootPosition.Undo(ref move);

            if (score > bestScore)
            {
                bestScore = score;
                bestMove = move;

                // Update PV
                pvTable[ply][0] = move;
                for (int j = 0; j < Max_Depth - ply - 1; j++)
                {
                    pvTable[ply][j + 1] = pvTable[ply + 1][j];
                }

                if (score > alpha)
                {
                    alpha = score;
                    
                    // Update history heuristic for quiet moves
                    if (move.IsQuiet)
                    {
                        moveOrdering.UpdateHistory(move, depth);
                    }
                }
            }

            if (alpha >= beta)
            {
                // Update killer moves for quiet moves
                if (move.IsQuiet)
                {
                    moveOrdering.UpdateKiller(move, ply);
                }
                break; // Beta cutoff
            }
        }

        // Store in transposition table
        byte nodeType = bestScore <= originalAlpha ? TranspositionTable.UpperBound :
                       bestScore >= beta ? TranspositionTable.LowerBound :
                       TranspositionTable.Exact;
        
        tt.Add(rootPosition.Hash, depth, bestScore, nodeType, bestMove);

        return bestScore;
    }

    private int Quiescence(int ply, int alpha, int beta, int qDepth)
    {
        if (ct.IsCancellationRequested) return 0;

        nodesSearched++;

        // Stand pat - can we already beat beta without any moves?
        int standPat = Evaluation.Evaluate(rootPosition);
        
        if (standPat >= beta)
        {
            return beta;
        }

        if (alpha < standPat)
        {
            alpha = standPat;
        }

        // Prevent excessive quiescence search depth
        if (qDepth >= Max_Quiescence_Depth)
        {
            return standPat;
        }

        // Generate only capture moves
        Span<Move> moves = stackalloc Move[218];
        int moveCount = MoveGenerator.Captures(rootPosition, ref moves);

        // Order captures by MVV-LVA
        moveOrdering.OrderMoves(moves, moveCount, Move.Null, ply);

        for (int i = 0; i < moveCount; i++)
        {
            var move = moves[i];

            // Delta pruning - skip captures that can't possibly raise alpha
            if (!Evaluation.IsMateScore(alpha))
            {
                const int queenValue = 900;
                int delta = standPat + queenValue + 200; // margin for positional gains
                
                if (move.CapturePieceType != PieceType.None)
                {
                    delta = standPat + Evaluation.Evaluate(rootPosition) + 200;
                }

                if (delta < alpha && move.PromotionPieceType == PieceType.None)
                {
                    continue; // Skip this capture
                }
            }

            rootPosition.Move(ref move);
            int score = -Quiescence(ply + 1, -beta, -alpha, qDepth + 1);
            rootPosition.Undo(ref move);

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