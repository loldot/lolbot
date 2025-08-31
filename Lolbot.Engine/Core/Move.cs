namespace Lolbot.Core;

public readonly struct Move : IEquatable<Move>
{
    // TODO: Fix color enum
    public const int C_Black = 0;
    public const int C_White = 1;

    public const int FromSqMask = 0x3f;

    public const int ToSqOffset = 6;
    public const int ToSqMask = 0x3f << ToSqOffset;

    public const int CaptSqOffset = 12;
    public const int CaptSqMask = 0x3f << CaptSqOffset;

    public const int ColorOffset = 18;
    public const int ColorMask = 1 << ColorOffset;

    public const int FromPieceOffset = 19;
    public const int FromPieceMask = 7 << FromPieceOffset;

    public const int CaptPieceOffset = 22;
    public const int CaptPieceMask = 7 << CaptPieceOffset;

    public const int PromotionPieceOffset = 25;
    public const int PromotionPieceMask = 7 << PromotionPieceOffset;

    public const int CastleOffset = 28;
    public const int CastleMask = 0xf << CastleOffset;


    public readonly uint value;

    public static readonly Move Null = new(0);

    public readonly byte FromIndex => (byte)(value & FromSqMask);
    public readonly byte ToIndex => (byte)((value & ToSqMask) >> ToSqOffset);
    public readonly byte CaptureIndex => (byte)((value & CaptSqMask) >> CaptSqOffset);
    public readonly byte Color => (byte)((value & ColorMask) >> ColorOffset);

    public readonly PieceType FromPieceType => (PieceType)((value & FromPieceMask) >> FromPieceOffset);
    public readonly Piece FromPiece => Utils.GetPiece(
        Color == 1 ? Colors.White : Colors.Black,
        FromPieceType
    );

    public readonly PieceType CapturePieceType => (PieceType)((value & CaptPieceMask) >> CaptPieceOffset);
    public readonly PieceType PromotionPieceType => (PieceType)((value & PromotionPieceMask) >> PromotionPieceOffset);
    public readonly Piece PromotionPiece => Utils.GetPiece(
        Color == 1 ? Colors.White : Colors.Black,
        PromotionPieceType
    );

    public readonly Piece CapturePiece
    {
        get
        {
            Colors c;

            if ((value & CastleMask) == 0) // Normal capture
            {
                c = Color == C_White ? Colors.Black : Colors.White;
            }
            else // Castle hack
            {
                c = Color == C_White ? Colors.White : Colors.Black;
            }

            return Utils.GetPiece(c, CapturePieceType);
        }
    }
    public readonly CastlingRights CastleFlag => (CastlingRights)((value & CastleMask) >> CastleOffset);

    public readonly ulong FromSquare => Squares.FromIndex(FromIndex);
    public readonly ulong ToSquare => Squares.FromIndex(ToIndex);
    public readonly ulong CaptureSquare => CapturePiece == Piece.None ? 0 : Squares.FromIndex(CaptureIndex);

    public readonly ulong CastleSquare => CastleFlag == CastlingRights.None ? 0 : Squares.FromIndex(CastleIndex);
    public readonly byte CastleIndex => CastleFlag switch
    {
        CastlingRights.WhiteKing => Squares.F1,
        CastlingRights.WhiteQueen => Squares.D1,
        CastlingRights.BlackKing => Squares.F8,
        CastlingRights.BlackQueen => Squares.D8,
        _ => 0
    };

    public readonly bool IsNull => value == 0;

    public bool IsQuiet => ((CaptPieceMask | PromotionPieceMask) & value) == 0;

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

    private Move(uint raw)
    {
        value = raw;
    }
    public Move(char fromPiece, string fromCoordinate, string toCoordinate)
    {
        var colorbit = Utils.GetColor(Utils.FromName(fromPiece)) == Colors.White ? 1u : 0u;
        value |= Squares.IndexFromCoordinate(fromCoordinate);
        value |= ((uint)Squares.IndexFromCoordinate(toCoordinate)) << ToSqOffset;
        value |= colorbit << ColorOffset;
        value |= (0xf & (uint)Utils.FromName(fromPiece)) << FromPieceOffset;
    }

    public Move(char fromPiece, string fromCoordinate, string toCoordinate, char capturePiece)
        : this(fromPiece, fromCoordinate, toCoordinate)
    {
        var cp = Utils.FromName(capturePiece);
        value |= (0xf & (uint)cp) << CaptPieceOffset;
        value |= cp != Piece.None ? ((uint)Squares.IndexFromCoordinate(toCoordinate)) << CaptSqOffset : 0;
    }

    public Move(Piece fromPiece, byte fromIndex, byte toIndex)
    {
        var colorbit = (uint)Utils.GetColor(fromPiece) & 1;
        value |= fromIndex;
        value |= ((uint)toIndex) << ToSqOffset;
        value |= colorbit << ColorOffset;
        value |= (0xf & (uint)fromPiece) << FromPieceOffset;
    }

    public Move(Piece fromPiece, byte fromIndex, byte toIndex, Piece capturePiece)
    {
        var colorbit = (uint)Utils.GetColor(fromPiece) & 1;
        value |= fromIndex;
        value |= ((uint)toIndex) << ToSqOffset;
        if (capturePiece != Piece.None)
            value |= ((uint)toIndex) << CaptSqOffset;

        value |= colorbit << ColorOffset;
        value |= (0xfu & (uint)fromPiece) << FromPieceOffset;
        value |= (0xfu & (uint)capturePiece) << CaptPieceOffset;
    }

    public Move(Piece fromPiece, byte fromIndex, byte toIndex, Piece capturePiece, Piece promotionPiece)
    {
        var colorbit = (uint)Utils.GetColor(fromPiece) & 1;
        value |= fromIndex;
        value |= ((uint)toIndex) << ToSqOffset;
        if (capturePiece != Piece.None)
            value |= ((uint)toIndex) << CaptSqOffset;

        value |= colorbit << ColorOffset;
        value |= (0xfu & (uint)fromPiece) << FromPieceOffset;
        value |= (0xfu & (uint)capturePiece) << CaptPieceOffset;
        value |= (0xfu & (uint)promotionPiece) << PromotionPieceOffset;
    }

    public Move(Piece fromPiece, byte fromIndex, byte toIndex, Piece capturePiece, byte captureIndex)
    {
        var colorbit = (uint)Utils.GetColor(fromPiece) & 1;
        value |= fromIndex;
        value |= ((uint)toIndex) << ToSqOffset;
        if (capturePiece != Piece.None)
            value |= ((uint)captureIndex) << CaptSqOffset;

        value |= colorbit << ColorOffset;
        value |= (0xfu & (uint)fromPiece) << FromPieceOffset;
        value |= (0xfu & (uint)capturePiece) << CaptPieceOffset;
    }

    public Move(Piece fromPiece, byte fromIndex, byte toIndex, Piece capturePiece, byte captureIndex, CastlingRights castle)
        : this(fromPiece, fromIndex, toIndex, capturePiece, captureIndex)
    {
        value |= ((uint)castle) << CastleOffset;
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
    public static Move Castle(Colors color)
        => color == Colors.White ? WhiteCastle : BlackCastle;
    public static Move QueenSideCastle(Colors color)
        => color == Colors.White ? WhiteQueenCastle : BlackQueenCastle;

    public static bool operator ==(Move left, Move right)
    {
        return left.value == right.value;
    }

    public static bool operator !=(Move left, Move right) => !(left == right);

    public override int GetHashCode() => (int)value;

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
