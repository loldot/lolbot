using static System.Math;

namespace Lolbot.Core;

public class Engine
{
    const int Max_Depth = 12;
    private static readonly TranspositionTable tt = new TranspositionTable();
    public static Game NewGame() => new Game();

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
        for (Piece i = Piece.WhitePawn; i < Piece.WhiteKing; i++)
        {
            eval += Heuristics.GetPieceValue(i, position[i]);
        }
        for (Piece i = Piece.BlackPawn; i < Piece.BlackKing; i++)
        {
            eval -= Heuristics.GetPieceValue(i, position[i]);
        }
        return eval;
    }

    public static Move? Reply(Game game)
    {
        var timer = new CancellationTokenSource(1_000);
        var bestMove = default(Move?);
        var depth = 1;

        while (depth <= Max_Depth && !timer.Token.IsCancellationRequested)
        {
            bestMove = BestMove(game, depth, timer.Token);
            Console.WriteLine($"{bestMove} after {depth}");
            depth++;
        }

        return bestMove;
    }

    public static Move? BestMove(Game game, int depth, CancellationToken ct)
    {
        Span<Move> legalMoves = stackalloc Move[218];

        var position = game.CurrentPosition;
        var count = MoveGenerator.Legal(ref position, ref legalMoves);
        legalMoves = legalMoves[..count];

        if (count == 0) return null;

        var bestEval = -999_999;
        var bestMove = legalMoves[0];

        var alpha = -999_999;
        var beta = 999_999;

        for (int i = 0; i < count; i++)
        {
            var move = legalMoves[i];
            var eval = -EvaluateMove(position.Move(move), depth, -beta, -alpha, 1);

            if (eval > bestEval)
            {
                bestEval = eval;
                bestMove = move;
                alpha = Max(alpha, eval);
            }
        }


        return bestMove;
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
        if (depth == 0) return color * Evaluate(position);

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
   
    private static int MoveComparer(Move x, Move y)
    {
        int score = 0;
        score -= Heuristics.GetPieceValue(x.PromotionPiece);
        score -= Heuristics.GetPieceValue(x.CapturePiece);

        score += Heuristics.GetPieceValue(y.PromotionPiece);
        score += Heuristics.GetPieceValue(y.CapturePiece);

        return score;
    }
}