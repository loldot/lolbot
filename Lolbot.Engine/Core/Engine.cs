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

    public static int Evaluate(Position position, int color)
    {
        var eval = 0;

        eval += Heuristics.Mobility(position, Color.White);
        eval -= Heuristics.Mobility(position, Color.Black);

        for (Piece i = Piece.WhitePawn; i < Piece.WhiteKing; i++)
        {
            eval += Heuristics.GetPieceValue(i, position[i]);
        }
        for (Piece i = Piece.BlackPawn; i < Piece.BlackKing; i++)
        {
            eval -= Heuristics.GetPieceValue(i, position[i]);
        }
        return color * eval;
    }

    public static Move? Reply(Game game)
    {
        var timer = new CancellationTokenSource(2_000);

        return Reply(game, timer.Token);
    }

    public static Move? Reply(Game game, CancellationToken ct)
    {
        var bestMove = default(Move?);
        var depth = 1;

        while (depth <= Max_Depth && !ct.IsCancellationRequested)
        {
            bestMove = BestMove(game, depth, bestMove, ct);
            depth++;
        }

        return bestMove;
    }

    public static Move? BestMove(Game game, int depth, Move? currentBest, CancellationToken ct)
    {
        Span<Move> legalMoves = stackalloc Move[218];

        var position = game.CurrentPosition;
        var count = MoveGenerator.Legal(ref position, ref legalMoves);
        legalMoves = legalMoves[..count];
        OrderMoves(ref legalMoves, ref currentBest);

        var bestEval = -999_999;
        var bestMove = currentBest;

        var alpha = -999_999;
        var beta = 999_999;

        var us = game.CurrentPlayer == Color.White ? 1 : -1;

        for (int i = 0; i < count; i++)
        {
            if (ct.IsCancellationRequested) break;
            
            var move = legalMoves[i];
            var eval = -EvaluateMove(position.Move(move), depth, -beta, -alpha, -us);

            if (eval > bestEval)
            {
                bestEval = eval;
                bestMove = move;
                alpha = Max(alpha, eval);
            }
        }
        Console.WriteLine($"info score cp {bestEval} depth {depth}");

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

    public static int EvaluateMove(Position position, int depth, int alpha, int beta, int color)
    {
        var eval = -999_999;
        var alphaOrig = alpha;

        if (tt.TryGet(position.Hash, depth, out var ttEntry))
        {
            if (ttEntry.Type == TranspositionTable.Exact)
                return ttEntry.Evaluation;
            else if (ttEntry.Type == TranspositionTable.Beta)
                alpha = Max(alpha, ttEntry.Evaluation);
            else if (ttEntry.Type == TranspositionTable.Alpha)
                beta = Min(beta, ttEntry.Evaluation);

            if (alpha >= beta)
                return ttEntry.Evaluation;
        }

        Span<Move> moves = stackalloc Move[218];
        var count = MoveGenerator.Legal(ref position, ref moves);

        if (count == 0) return eval - depth;
        if (depth == 0) return QuiesenceSearch(position, alpha, beta, color);

        moves = moves[..count];
        moves.Sort(MoveComparer);

        for (byte i = 0; i < count; i++)
        {
            eval = Max(eval, -EvaluateMove(position.Move(moves[i]), depth - 1, -beta, -alpha, -color));
            alpha = Max(eval, alpha);
            if (alpha >= beta) break;
        }

        byte ttType;
        if (eval <= alphaOrig) ttType = TranspositionTable.Alpha;
        else if (eval >= beta) ttType = TranspositionTable.Beta;
        else ttType = TranspositionTable.Exact;

        tt.Add(position.Hash, depth, eval, ttType, new Move());

        return eval;
    }

    private static int QuiesenceSearch(Position position, int alpha, int beta, int color)
    {
        var standPat = Evaluate(position, color);

        if (standPat >= beta) return beta;
        if (alpha < standPat) alpha = standPat;

        Span<Move> moves = stackalloc Move[218];

        var count = MoveGenerator.Captures(ref position, ref moves);
        moves = moves[..count];
        moves.Sort(MoveComparer);

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