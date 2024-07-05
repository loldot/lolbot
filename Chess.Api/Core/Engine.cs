using System.Numerics;
using System.Runtime.InteropServices;

namespace Lolbot.Core;

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
    public readonly Piece PromotionPiece = Piece.None;

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

    private static readonly Move WhiteCastle = new(4, 6, 7, 5, Piece.WhiteRook, Piece.None);
    private static readonly Move BlackCastle = new(60, 62, 63, 61, Piece.BlackRook, Piece.None);

    private static readonly Move WhiteQueenCastle = new(4, 2, 0, 3, Piece.WhiteRook, Piece.None);
    private static readonly Move BlackQueenCastle = new(60, 62, 63, 61, Piece.BlackRook, Piece.None);


    // TODO: Fisher castling rules :cry:
    public static Move Castle(Color color)
        => color == Color.White ? WhiteCastle : BlackCastle;
    public static Move QueenSideCastle(Color color)
        => color == Color.White ? WhiteQueenCastle : BlackQueenCastle;

    public override string ToString()
    {
        return $"{Squares.CoordinateFromIndex(FromIndex)}"
            + ((CapturePiece != Piece.None) ? "x" : "")
            + $"{Squares.CoordinateFromIndex(ToIndex)}";
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

    public int PlyCount => Moves.Length;
    public Color CurrentPlayer => (Color)(PlyCount % 2 + 1);
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

    private static Piece GetCapture(Game game, Square to)
    {
        return game.CurrentPosition.GetOccupant(Squares.ToIndex(to));
    }

    public static int Evaluate(Position position)
    {
        return Bitboards.CountOccupied(position.WhitePawns) * 100
            + Bitboards.CountOccupied(position.WhiteKnights) * 300
            + Bitboards.CountOccupied(position.WhiteBishops) * 325
            + Bitboards.CountOccupied(position.WhiteRooks) * 500
            + Bitboards.CountOccupied(position.BlackQueens) * 900

            + Bitboards.CountOccupied(position.BlackPawns) * -100
            + Bitboards.CountOccupied(position.BlackKnights) * -300
            + Bitboards.CountOccupied(position.BlackBishops) * -325
            + Bitboards.CountOccupied(position.BlackRooks) * -500
            + Bitboards.CountOccupied(position.BlackQueens) * -900;
    }
}