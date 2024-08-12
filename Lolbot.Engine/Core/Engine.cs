using static System.Math;

namespace Lolbot.Core;

public static class Engine
{
    const int Max_Depth = 64;
    private static readonly TranspositionTable tt = new TranspositionTable();

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

        // if (position.IsCheck)
        // {
        //     eval -= position.CurrentPlayer == Color.White ? 50 : -50;
        // }

        eval += Heuristics.Mobility(position, Color.White);
        eval -= Heuristics.Mobility(position, Color.Black);

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

    public static Move? BestMove(Game game)
    {
        var timer = new CancellationTokenSource(2_000);

        return BestMove(game, timer.Token);
    }

    public static Move? BestMove(Game game, CancellationToken ct)
    {
        var bestMove = default(Move?);
        var depth = 1;

        while (depth <= Max_Depth && !ct.IsCancellationRequested)
        {
            bestMove = BestMove(game, depth, bestMove, ct);
            depth++;
        }

        Console.WriteLine($"info tt fill factor: {tt.FillFactor:P3}");
        Console.WriteLine($"info tt set count: {tt.set_count}");
        Console.WriteLine($"info tt rewrite count: {tt.rewrite_count}");
        Console.WriteLine($"info tt collision count: {tt.collision_count}");


        return bestMove;
    }

    public static Move? BestMove(Game game, int depth, Move? currentBest, CancellationToken ct)
    {
        Span<Move> moves = stackalloc Move[218];

        var history = game.RepetitionTable;
        var position = game.CurrentPosition;

        var count = MoveGenerator.Legal(ref position, ref moves);
        moves = moves[..count];
        OrderMoves(ref moves, ref currentBest);

        var bestEval = -999_999;
        var bestMove = currentBest;

        var alpha = -999_999;
        var beta = 999_999;

        for (int i = 0; i < count; i++)
        {
            if (ct.IsCancellationRequested) break;

            var move = moves[i];
            var nextPosition = position.Move(move);

            history.Update(move, nextPosition.Hash);
            var eval = -EvaluateMove(history, ref nextPosition, depth, -beta, -alpha, -1);
            history.Unwind();

            if (eval > bestEval)
            {
                bestEval = eval;
                bestMove = move;
                alpha = Max(alpha, eval);
            }
        }
        Console.WriteLine($"info score cp {bestEval} depth {depth} bm {bestMove}");

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

        Span<Move> moves = stackalloc Move[218];
        var count = MoveGenerator.Legal(ref position, ref moves);

        if (count == 0) return position.IsCheck ? (-16384 - depth) : 0;
        if (depth == 0) return QuiesenceSearch(position, alpha, beta, color);

        moves = moves[..count];
        moves.Sort(MoveComparer);

        for (byte i = 0; i < count; i++)
        {
            var nextPosition = position.Move(moves[i]);
            history.Update(moves[i], nextPosition.Hash);

            eval = Max(eval, -EvaluateMove(history, ref nextPosition, depth - 1, -beta, -alpha, -color));

            history.Unwind();
            alpha = Max(eval, alpha);
            if (alpha >= beta) break;
        }

        byte ttType;
        if (eval <= alphaOrig) ttType = TranspositionTable.UpperBound;
        else if (eval >= beta) ttType = TranspositionTable.LowerBound;
        else ttType = TranspositionTable.Exact;

        tt.Add(position.Hash, depth, eval, ttType);

        return eval;
    }

    private static int QuiesenceSearch(Position position, int alpha, int beta, int color)
    {
        Span<Move> moves = stackalloc Move[218];

        var count = MoveGenerator.Captures(ref position, ref moves);
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