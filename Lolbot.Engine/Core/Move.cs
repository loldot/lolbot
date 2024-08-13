using System.Runtime.CompilerServices;

namespace Lolbot.Core;

public readonly struct Move : IEquatable<Move>
{
    public readonly Piece FromPiece;
    public readonly byte FromIndex;
    public readonly byte ToIndex;
    public readonly Piece CapturePiece;
    public readonly byte CaptureIndex;
    public readonly Piece PromotionPiece;
    public readonly CastlingRights CastleFlag;

    public readonly ulong FromSquare => Squares.FromIndex(in FromIndex);
    public readonly ulong ToSquare => Squares.FromIndex(in ToIndex);
    public readonly ulong CaptureSquare => CapturePiece == Piece.None ? 0 : Squares.FromIndex(in CaptureIndex);

    public readonly ulong CastleSquare => CastleFlag == CastlingRights.None ? 0 : Squares.FromIndex(CastleIndex);
    public readonly byte CastleIndex => CastleFlag switch
    {
        CastlingRights.WhiteKing => Squares.F1,
        CastlingRights.WhiteQueen => Squares.D1,
        CastlingRights.BlackKing => Squares.F8,
        CastlingRights.BlackQueen => Squares.D8,
        _ => 0
    };

    public static Move Promote(char fromPiece, string fromCoordinate, string toCoordinate, char toPiece)
    {
        return new Move(
            Utils.FromName(fromPiece),
            Squares.IndexFromCoordinate(fromCoordinate),
            Squares.IndexFromCoordinate(toCoordinate),
            Piece.None,
            Utils.FromName(toPiece)
        );
    }

    public static Move PromoteWithCapture(char fromPiece, string fromCoordinate, string toCoordinate, char capturePiece, char toPiece)
    {
        return new Move(
            Utils.FromName(fromPiece),
            Squares.IndexFromCoordinate(fromCoordinate),
            Squares.IndexFromCoordinate(toCoordinate),
            Utils.FromName(capturePiece),
            Utils.FromName(toPiece)
        );
    }


    public Move(char fromPiece, string fromCoordinate, string toCoordinate) : this()
    {
        FromPiece = Utils.FromName(fromPiece);
        FromIndex = Squares.IndexFromCoordinate(fromCoordinate);
        ToIndex = Squares.IndexFromCoordinate(toCoordinate); ;
    }

    public Move(char fromPiece, string fromCoordinate, string toCoordinate, char capturePiece) : this()
    {
        FromPiece = Utils.FromName(fromPiece);
        FromIndex = Squares.IndexFromCoordinate(fromCoordinate);
        ToIndex = Squares.IndexFromCoordinate(toCoordinate);
        CaptureIndex = Squares.IndexFromCoordinate(toCoordinate);
        CapturePiece = Utils.FromName(capturePiece);
    }

    public Move(Piece fromPiece, byte fromIndex, byte toIndex) : this()
    {
        FromPiece = fromPiece;
        FromIndex = fromIndex;
        ToIndex = toIndex;
    }

    public Move(Piece fromPiece, byte fromIndex, byte toIndex, Piece capturePiece)
        : this(fromPiece, fromIndex, toIndex)
    {
        CapturePiece = capturePiece;
        CaptureIndex = capturePiece != Piece.None ? toIndex : (byte)0;;
    }

    public Move(Piece fromPiece, byte fromIndex, byte toIndex, Piece capturePiece, Piece promotionPiece)
        : this(fromPiece, fromIndex, toIndex)
    {
        CapturePiece = capturePiece;
        CaptureIndex = capturePiece != Piece.None ? toIndex : (byte)0;
        PromotionPiece = promotionPiece;
    }

    public Move(Piece fromPiece, byte fromIndex, byte toIndex, Piece capturePiece, byte captureIndex)
        : this(fromPiece, fromIndex, toIndex)
    {
        CapturePiece = capturePiece;
        CaptureIndex = captureIndex;
    }

    public Move(Piece fromPiece, byte fromIndex, byte toIndex, Piece capturePiece, byte captureIndex, CastlingRights castle)
        : this(fromPiece, fromIndex, toIndex)
    {
        CapturePiece = capturePiece;
        CaptureIndex = captureIndex;
        CastleFlag = castle;
    }

    private static readonly Move WhiteCastle = new(
        Piece.WhiteKing, Squares.E1,
        Squares.G1,
        Piece.WhiteRook, Squares.H1,
        CastlingRights.WhiteKing
    );

    private static readonly Move BlackCastle = new(
        Piece.BlackKing, Squares.E8,
        Squares.G8,
        Piece.BlackRook, Squares.H8,
        CastlingRights.BlackKing
    );

    private static readonly Move WhiteQueenCastle = new(
        Piece.WhiteKing, Squares.E1,
        Squares.C1,
        Piece.WhiteRook, Squares.A1,
        CastlingRights.WhiteQueen
    );
    private static readonly Move BlackQueenCastle = new(
        Piece.BlackKing, Squares.E8,
        Squares.C8,
        Piece.BlackRook, Squares.A8,
        CastlingRights.BlackQueen
    );


    // TODO: Fisher castling rules :cry:
    public static Move Castle(Color color)
        => color == Color.White ? WhiteCastle : BlackCastle;
    public static Move QueenSideCastle(Color color)
        => color == Color.White ? WhiteQueenCastle : BlackQueenCastle;

    public static bool operator ==(Move left, Move right)
    {
        return left.FromIndex == right.FromIndex
            && left.FromPiece == right.FromPiece

            && left.ToIndex == right.ToIndex

            && left.CaptureIndex == right.CaptureIndex
            && left.CapturePiece == right.CapturePiece
            && left.PromotionPiece == right.PromotionPiece
            && left.CastleFlag == right.CastleFlag;
    }

    public static bool operator !=(Move left, Move right) => !(left == right);

    public override int GetHashCode() =>
        FromIndex << 26 ^
        ToIndex << 20 ^
        CaptureIndex << 12 ^
        (byte)PromotionPiece ^
        (byte)CastleFlag;

    public override bool Equals(object? obj)
    {
        if (obj is Move other) return Equals(other);
        return false;
    }
    public bool Equals(Move other) => this == other;

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
}
