using System.Collections.Immutable;
using static System.Math;

namespace Lolbot.Core;

[Flags]
public enum Castle : byte
{
    None = 0,
    WhiteQueen = 1,
    WhiteKing = 2,
    BlackQueen = 4,
    BlackKing = 8,
    All = WhiteKing | WhiteQueen | BlackKing | BlackQueen
}
public enum Color : byte { None = 0, White = 1, Black = 2 }
public enum Piece : byte
{
    None = 0,

    WhitePawn = 0x11,
    WhiteKnight = 0x12,
    WhiteBishop = 0x13,
    WhiteRook = 0x14,
    WhiteQueen = 0x15,
    WhiteKing = 0x16,

    BlackPawn = 0x21,
    BlackKnight = 0x22,
    BlackBishop = 0x23,
    BlackRook = 0x24,
    BlackQueen = 0x25,
    BlackKing = 0x26,
}


public record Game(Position InitialPosition, Move[] Moves)
{
    public Game() : this(new Position(), []) { }

    public Game(string fen) : this(Position.FromFen(fen), [])
    {
    }

    public int PlyCount => Moves.Length;
    public Color CurrentPlayer => CurrentPosition.CurrentPlayer;
    public Position CurrentPosition => GetPosition(InitialPosition, Moves);

    public Position GetPosition(Position position, Move[] moves)
    {
        foreach (var m in moves) position = position.Move(m);

        return position;
    }

    public bool IsLegalMove(Move move)
    {
        return CurrentPosition
            .GenerateLegalMoves().ToArray()
            .Contains(move);
    }
}


public class Engine
{
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
        const int DEPTH = 5;

        var legalMoves = game.CurrentPosition.GenerateLegalMoves().ToArray();
        var evals = new int[legalMoves.Length];

        if (legalMoves.Length == 0) return null;

        Parallel.For(0, legalMoves.Length, i =>
        {
            evals[i] = EvaluateMove(game.CurrentPosition.Move(legalMoves[i]), DEPTH);
        });

        var bestEval = 999_999;
        var bestMove = legalMoves[0];

        for (int i = 0; i < legalMoves.Length; i++)
        {
            if (evals[i] < bestEval)
            {
                bestEval = evals[i];
                bestMove = legalMoves[i];
            }
        }

        Console.WriteLine($"Eval: {bestEval} [{string.Join(", ", bestMove)}]");

        return bestMove;
    }

    public static int EvaluateMove(Position position, int remainingDepth)
    {
        if (remainingDepth == 0) return Evaluate(position);

        var (alpha, beta) = (-999_999, 999_999);

        if (position.CurrentPlayer == Color.White)
        {
            return AlphaBetaMax(position, alpha, beta, remainingDepth);
        }
        else
        {
            return AlphaBetaMin(position, alpha, beta, remainingDepth);
        }
    }

    private static int AlphaBetaMax(Position position, int alpha, int beta, int remainingDepth)
    {
        if (remainingDepth == 0) return Evaluate(position);
        foreach (var candidate in position.GenerateLegalMoves())
        {
            var score = AlphaBetaMin(position.Move(candidate), alpha, beta, remainingDepth - 1);
            if (score >= beta)
                return beta;   // fail hard beta-cutoff
            if (score > alpha)
                alpha = score; // alpha acts like max in MiniMax
        }
        return alpha;
    }

    private static int AlphaBetaMin(Position position, int alpha, int beta, int remainingDepth)
    {
        if (remainingDepth == 0) return Evaluate(position);

        var candidateMoves = position.GenerateLegalMoves();
        candidateMoves.Sort(MoveComparer);

        foreach (var candidate in candidateMoves)
        {
            var score = AlphaBetaMax(position.Move(candidate), alpha, beta, remainingDepth - 1);
            if (score <= alpha)
                return alpha; // fail hard alpha-cutoff
            if (score < beta)
                beta = score; // beta acts like min in MiniMax
        }
        return beta;
    }

    private static int MoveComparer(Move x, Move y)
    {
        int score = 0;
        score += Heuristics.GetPieceValue(x.PromotionPiece);
        score += Heuristics.GetPieceValue(x.CapturePiece);

        score -= Heuristics.GetPieceValue(x.PromotionPiece);
        score -= Heuristics.GetPieceValue(x.CapturePiece);

        return score;
    }
}