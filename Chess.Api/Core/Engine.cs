using System.Collections.Immutable;
using System.Numerics;
using System.Runtime.InteropServices;
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


[StructLayout(LayoutKind.Sequential)]
public readonly struct Move : IEquatable<Move>
{
    public readonly byte FromIndex;
    public readonly byte ToIndex;
    public readonly byte CaptureIndex = 0;
    public readonly byte CastleIndex = 0;
    public readonly Piece CapturePiece = Piece.None;
    public Piece PromotionPiece { get; init; } = Piece.None;

    public Square FromSquare => Squares.FromIndex(FromIndex);
    public Square ToSquare => Squares.FromIndex(ToIndex);
    public Square CaptureSquare => Squares.FromIndex(CaptureIndex);

    public Move(string from, string to) : this(
        Squares.FromCoordinates(from),
        Squares.FromCoordinates(to)
    )
    { }

    public Move(string from, string to, string captureSquare, char capturePiece) : this(
        Squares.IndexFromCoordinate(from),
        Squares.IndexFromCoordinate(to),
        Squares.IndexFromCoordinate(captureSquare),
        Utils.FromName(capturePiece)
    )
    { }

    public Move(Square from, Square to)
    {
        FromIndex = (byte)BitOperations.Log2(from);
        ToIndex = (byte)BitOperations.Log2(to);
    }

    public Move(byte fromIndex, byte toIndex)
    : this(fromIndex, toIndex, 0, 0, Piece.None, Piece.None) { }

    public Move(byte fromIndex, byte toIndex, byte captureIndex, Piece capturePiece)
    : this(fromIndex, toIndex, captureIndex, 0, capturePiece, Piece.None) { }


    public Move(
        byte fromIndex,
        byte toIndex,
        byte captureIndex,
        byte castleIndex,
        Piece capturePiece,
        Piece promotionPiece)
    {
        FromIndex = fromIndex;
        ToIndex = toIndex;
        CaptureIndex = captureIndex;
        CastleIndex = castleIndex;
        CapturePiece = capturePiece;
        PromotionPiece = promotionPiece;
    }

    private static readonly Move WhiteCastle = new(
        Squares.E1,
        Squares.G1,
        Squares.H1,
        Squares.F1, Piece.WhiteRook, Piece.None);
    private static readonly Move BlackCastle = new(
        Squares.E8,
        Squares.G8,
        Squares.H8,
        Squares.F8, Piece.BlackRook, Piece.None);

    private static readonly Move WhiteQueenCastle = new(
        Squares.E1,
        Squares.C1,
        Squares.A1,
        Squares.D1, Piece.WhiteRook, Piece.None);
    private static readonly Move BlackQueenCastle = new(
        Squares.E8,
        Squares.C8,
        Squares.A8,
        Squares.D8, Piece.BlackRook, Piece.None);


    // TODO: Fisher castling rules :cry:
    public static Move Castle(Color color)
        => color == Color.White ? WhiteCastle : BlackCastle;
    public static Move QueenSideCastle(Color color)
        => color == Color.White ? WhiteQueenCastle : BlackQueenCastle;

    public override string ToString()
    {
        if (this == WhiteCastle) return "O-O";
        if (this == WhiteQueenCastle) return "O-O-O";
        if (this == BlackCastle) return "o-o";
        if (this == BlackQueenCastle) return "o-o-o";

        return $"{Squares.CoordinateFromIndex(FromIndex)}"
            + ((CapturePiece != Piece.None) ? "x" : "")
            + $"{Squares.CoordinateFromIndex(ToIndex)}"
            + (PromotionPiece != Piece.None ? $"={Utils.PieceName(PromotionPiece)}" : "");
    }

    public bool Equals(Move other)
    {
        return FromIndex == other.FromIndex
            && ToIndex == other.ToIndex
            && CaptureIndex == other.CaptureIndex
            && CastleIndex == other.CastleIndex
            && CapturePiece == other.CapturePiece
            && PromotionPiece == other.PromotionPiece;
    }

    public override bool Equals(object? obj)
    {
        return obj is Move && Equals((Move)obj);
    }

    public static bool operator ==(Move left, Move right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(Move left, Move right)
    {
        return !(left == right);
    }

    public override int GetHashCode()
    {
        int firstHalf = FromIndex << 26 | ToIndex << 20;
        int secondHalf = CastleIndex + CaptureIndex + (int)CapturePiece + (int)PromotionPiece;
        return firstHalf | (secondHalf & 0xffffff);
    }
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
            .GenerateLegalMoves(CurrentPlayer).ToArray()
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
            .GenerateLegalMoves(game.CurrentPlayer)
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
        return Bitboards.CountOccupied(position.WhitePawns) * 100
            + Bitboards.CountOccupied(position.WhiteKnights) * 300
            + Bitboards.CountOccupied(position.WhiteBishops) * 325
            + Bitboards.CountOccupied(position.WhiteRooks) * 500
            + Bitboards.CountOccupied(position.WhiteQueens) * 900

            + Bitboards.CountOccupied(position.BlackPawns) * -100
            + Bitboards.CountOccupied(position.BlackKnights) * -300
            + Bitboards.CountOccupied(position.BlackBishops) * -325
            + Bitboards.CountOccupied(position.BlackRooks) * -500
            + Bitboards.CountOccupied(position.BlackQueens) * -900;
    }

    public static Move? Reply(Game game)
    {
        const int DEPTH = 2;

        var legalMoves = game.CurrentPosition.GenerateLegalMoves().ToArray();
        var evals = new int[legalMoves.Length];

        if (legalMoves.Length == 0) return null;

        Parallel.For(0, legalMoves.Length, i =>
        {
            evals[i] = EvaluateMove(game.CurrentPosition.Move(legalMoves[i]), DEPTH, true);
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

    public static int EvaluateMove(Position position, int remainingDepth, bool isWhite)
    {
        if (remainingDepth < 0) return Evaluate(position);

        var legalMoves = position.GenerateLegalMoves();

        if (isWhite)
        {
            var value = -999_999;
            foreach (var candidate in legalMoves)
            {
                value = Max(value, EvaluateMove(position.Move(candidate), remainingDepth - 1, false));
            }

            return value;
        }
        else
        {
            var value = 999_999;
            foreach (var candidate in legalMoves)
            {
                value = Min(value, EvaluateMove(position.Move(candidate), remainingDepth - 1, false));
            }
            return value;
        }
    }
}