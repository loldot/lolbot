using System.Runtime.CompilerServices;
using static System.Math;

namespace Lolbot.Core;

public static class Engine
{
    const int Max_Depth = 64;
    private static readonly TranspositionTable tt = new TranspositionTable();
    private static readonly int[] historyHeuristic = new int[4096];
    private static int nodes = 0;

    public static void Init()
    {
        Console.WriteLine("info " + MovePatterns.PextTable.Length);
    }

    public static Game NewGame() => new Game();

    public static Game FromPosition(string fenstring)
    {
        return new Game(Position.FromFen(fenstring), []);
    }

    public static Game Move(Game game, string from, string to)
    {
        return Move(
            game,
            Squares.FromCoordinates(from),
            Squares.FromCoordinates(to)
        );
    }

    public static Game Move(Game game, Square from, Square to)
    {
        var move = game.CurrentPosition
            .GenerateLegalMoves()
            .ToArray()
            .FirstOrDefault(x => x.FromIndex == Squares.ToIndex(from) && x.ToIndex == Squares.ToIndex(to));
        return Move(game, move);
    }

    public static Game Move(Game game, Move move)
    {
        if (!game.IsLegalMove(move)) throw new ArgumentException("Invalid move");

        return new Game(game.InitialPosition, [.. game.Moves, move]);
    }


    public static int Evaluate(Position position)
    {
        var eval = 0;
        int color = position.CurrentPlayer == Color.White ? 1 : -1;

        if (position.IsCheck)
        {
            eval -= color * 50;
        }

        eval += Heuristics.Mobility(position, Color.White);
        eval -= Heuristics.Mobility(position, Color.Black);

        eval += Heuristics.IsolatedPawns(position, Color.White);
        eval -= Heuristics.IsolatedPawns(position, Color.Black);

        for (Piece i = Piece.WhitePawn; i < Piece.WhiteKing; i++)
        {
            eval += Heuristics.GetPieceValue(i, position[i], position.Occupied);
        }
        for (Piece i = Piece.BlackPawn; i < Piece.BlackKing; i++)
        {
            eval -= Heuristics.GetPieceValue(i, position[i], position.Occupied);
        }
        return color * eval;
    }

    public static int Perft(in Position position, int remainingDepth = 4, int split = 0)
    {
        Span<Move> moves = stackalloc Move[218];
        var currentCount = MoveGenerator.Legal(in position, ref moves);
        var count = 0;

        if (remainingDepth == 1) return currentCount;

        for (int i = 0; i < currentCount; i++)
        {
            var posCount = Perft(position.Move(moves[i]), remainingDepth - 1);
            if (remainingDepth == split) Console.WriteLine($"{moves[i]}: {posCount}");
            count += posCount;
        }
        return count;
    }

    public static Move? BestMove(Game game)
    {
        var timer = new CancellationTokenSource(2_000);

        return BestMove(game, timer.Token);
    }

    public static Move? BestMove(Game game, CancellationToken ct)
    {
        // Age history heuristic
        for (int i = 0; i < historyHeuristic.Length; i++)
            historyHeuristic[i] /= 8;

        var bestMove = default(Move?);
        var depth = 1;

        while (depth <= Max_Depth && !ct.IsCancellationRequested)
        {
            bestMove = BestMove(game, depth, bestMove, ct);
            depth++;
        }
#if DEBUG
        Console.WriteLine($"info tt fill factor: {tt.FillFactor:P3}");
        Console.WriteLine($"info tt set count: {tt.set_count}");
        Console.WriteLine($"info tt rewrite count: {tt.rewrite_count}");
        Console.WriteLine($"info tt collision count: {tt.collision_count}");
#endif

        return bestMove;
    }

    public static Move? BestMove(Game game, int depth, Move? currentBest, CancellationToken ct)
    {
        nodes = 0;

        Span<Move> moves = stackalloc Move[218];

        var history = game.RepetitionTable;
        var position = game.CurrentPosition;

        if (position.IsCheck) depth++;

        var count = MoveGenerator.Legal(ref position, ref moves);
        moves = moves[..count];
        OrderMoves(ref moves, ref currentBest);

        var bestEval = -999_999;
        var bestMove = currentBest;

        var (alpha, beta) = (-999_999, 999_999);
        int i = 0;
        for (; i < count; i++)
        {
            if (ct.IsCancellationRequested) break;

            var move = moves[i];
            var nextPosition = position.Move(move);

            int eval;
            history.Update(move, nextPosition.Hash);
            if (i == 0)
            {
                eval = -EvaluateMove(history, ref nextPosition, depth, -beta, -alpha, -1);
            }
            else
            {
                eval = -EvaluateMove(history, ref nextPosition, depth, -alpha - 1, -alpha, -1);
                if (eval > alpha && beta - alpha > 1)
                    eval = -EvaluateMove(history, ref nextPosition, depth, -beta, -alpha, -1);
            }

            history.Unwind();

            if (eval > bestEval)
            {
                bestEval = eval;
                bestMove = move;
                alpha = Max(alpha, eval);
            }
        }
        nodes += i;
        Console.WriteLine($"info score cp {bestEval} depth {depth} bm {bestMove} nodes {nodes}");

        return bestMove;
    }

    private static void OrderMoves(ref Span<Move> legalMoves, ref Move? currentBest)
    {
        legalMoves.Sort(MoveComparer);

        if (currentBest is not null && legalMoves.Length > 1)
        {
            var index = legalMoves.IndexOf(currentBest.Value);
            legalMoves[index] = legalMoves[0];
            legalMoves[0] = currentBest.Value;
        }
    }

    public static int EvaluateMove(RepetitionTable history, ref Position position, int depth, int alpha, int beta, int color)
    {
        var eval = -999_999;
        var alphaOrig = alpha;

        if (history.IsDrawByRepetition(position.Hash)) return 0;
        if (tt.TryGet(position.Hash, depth, out var ttEntry))
        {
            if (ttEntry.Type == TranspositionTable.Exact)
                return ttEntry.Evaluation;
            else if (ttEntry.Type == TranspositionTable.LowerBound)
                alpha = Max(alpha, ttEntry.Evaluation);
            else if (ttEntry.Type == TranspositionTable.UpperBound)
                beta = Min(beta, ttEntry.Evaluation);

            if (alpha >= beta)
                return ttEntry.Evaluation;
        }
        else if (depth > 3) depth--;

        Span<Move> moves = stackalloc Move[218];
        var count = MoveGenerator.Legal(ref position, ref moves);

        if (count == 0) return position.IsCheck ? (-16384 - depth) : 0;
        if (depth == 0) return QuiesenceSearch(position, alpha, beta, color);

        moves = moves[..count];
        moves.Sort(MoveComparer);

        byte i = 0;
        for (; i < count; i++)
        {
            var nextPosition = position.Move(moves[i]);
            history.Update(moves[i], nextPosition.Hash);

            eval = Max(eval, -EvaluateMove(history, ref nextPosition, depth - 1, -beta, -alpha, -color));

            history.Unwind();
            alpha = Max(eval, alpha);
            if (alpha >= beta)
            {
                if (moves[i].CapturePiece == Piece.None) historyHeuristic[64 * moves[i].FromIndex + moves[i].ToIndex] = depth * depth;
                break;
            }
        }
        nodes += i;

        byte ttType;
        if (eval <= alphaOrig) ttType = TranspositionTable.UpperBound;
        else if (eval >= beta) ttType = TranspositionTable.LowerBound;
        else ttType = TranspositionTable.Exact;

        tt.Add(position.Hash, depth, eval, ttType);

        return eval;
    }

    private static int QuiesenceSearch(in Position position, int alpha, int beta, int color)
    {
        Span<Move> moves = stackalloc Move[218];

        var count = MoveGenerator.Captures(in position, ref moves);
        moves = moves[..count];
        moves.Sort(MoveComparer);

        var standPat = Evaluate(position);

        if (standPat >= beta) return beta;
        if (alpha < standPat) alpha = standPat;

        for (byte i = 0; i < count; i++)
        {
            var eval = -QuiesenceSearch(position.Move(moves[i]), -beta, -alpha, -color);

            if (eval >= beta) return beta;

            alpha = Max(eval, alpha);
        }

        return alpha;
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int MoveComparer(Move x, Move y)
    {
        int score = 0;

        score -= Heuristics.GetPieceValue(x.PromotionPiece);
        score -= Heuristics.MVV_LVA(x.CapturePiece, x.FromPiece);
        score -= historyHeuristic[64 * x.FromIndex + x.ToIndex];

        score += Heuristics.GetPieceValue(y.PromotionPiece);
        score += Heuristics.MVV_LVA(y.CapturePiece, y.FromPiece);
        score += historyHeuristic[64 * y.FromIndex + y.ToIndex];

        return score;
    }

    internal static void Reset()
    {
        Array.Clear(historyHeuristic);
    }
}