using System.Numerics;
using System.Runtime.InteropServices;

namespace Lolbot.Core;

[StructLayout(LayoutKind.Sequential)]
public readonly struct Move : IEquatable<Move>
{
    public readonly byte FromIndex;
    public readonly byte ToIndex;
    public readonly byte CaptureIndex = 0;
    public readonly byte CastleIndex = 0;
    public readonly Piece CapturePiece = Piece.None;
    public Piece PromotionPiece { get; init; } = Piece.None;

    public Square FromSquare => Squares.FromIndex(in FromIndex);
    public Square ToSquare => Squares.FromIndex(in ToIndex);
    public Square CaptureSquare => Squares.FromIndex(in CaptureIndex);
    public Square CastleSquare => Squares.FromIndex(in CastleIndex);


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

        return $"{Squares.ToCoordinate(FromSquare)}"
            + ((CapturePiece != Piece.None) ? "x" : "")
            + $"{Squares.ToCoordinate(ToSquare)}"
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
