using System.Collections.Specialized;
using System.Runtime.CompilerServices;

namespace Lolbot.Core;

public readonly struct Move : IEquatable<Move>
{
    public static readonly BitVector32.Section from = BitVector32.CreateSection(63);
    public static readonly BitVector32.Section to = BitVector32.CreateSection(63, from);
    public static readonly BitVector32.Section capture = BitVector32.CreateSection(63, to);
    public static readonly BitVector32.Section color = BitVector32.CreateSection(1, capture);
    public static readonly BitVector32.Section fromPiece = BitVector32.CreateSection(6, color);
    public static readonly BitVector32.Section capturePiece = BitVector32.CreateSection(6, fromPiece);
    public static readonly BitVector32.Section promotionPiece = BitVector32.CreateSection(6, capturePiece);
    public static readonly BitVector32.Section castling = BitVector32.CreateSection(8, promotionPiece);

    public readonly BitVector32 value;

    public static readonly Move Null = new(0);

    public readonly Piece FromPiece => Utils.GetPiece(value[color] == 1 ? Color.White : Color.Black, (PieceType)value[fromPiece]);
    public readonly byte FromIndex => (byte)value[from];
    public readonly byte ToIndex => (byte)value[to];
    public readonly Piece CapturePiece
    {
        get
        {
            Color c;
            if (value[castling] == 0)
            {
                c = value[color] == 0 ? Color.White : Color.Black;
            }
            else
            {
                c = value[color] == 1 ? Color.White : Color.Black;
            }

            return Utils.GetPiece(c, (PieceType)value[capturePiece]);
        }
    }
    public readonly byte CaptureIndex => (byte)value[capture];
    public readonly Piece PromotionPiece => Utils.GetPiece(value[color] == 1 ? Color.White : Color.Black, (PieceType)value[promotionPiece]);
    public readonly CastlingRights CastleFlag => (CastlingRights)value[castling];

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

    private Move(int raw)
    {
        value = new BitVector32(raw);
    }
    public Move(char fromPiece, string fromCoordinate, string toCoordinate) : this()
    {
        value[color] = Utils.GetColor(Utils.FromName(fromPiece)) == Color.White ? 1 : 0;
        value[Move.fromPiece] = 0xf & (int)Utils.FromName(fromPiece);
        value[from] = Squares.IndexFromCoordinate(fromCoordinate);
        value[to] = Squares.IndexFromCoordinate(toCoordinate);
    }

    public Move(char fromPiece, string fromCoordinate, string toCoordinate, char capturePiece) : this()
    {
        value[color] = Utils.GetColor(Utils.FromName(fromPiece)) == Color.White ? 1 : 0;
        value[Move.fromPiece] = 0xf & (int)Utils.FromName(fromPiece);
        value[from] = Squares.IndexFromCoordinate(fromCoordinate);
        value[to] = Squares.IndexFromCoordinate(toCoordinate);


        var cp = Utils.FromName(capturePiece);
        value[Move.capturePiece] = 0xf & (int)cp;
        value[capture] = cp != Piece.None ? Squares.IndexFromCoordinate(toCoordinate) : (byte)0; ;
    }

    public Move(Piece fromPiece, byte fromIndex, byte toIndex) : this()
    {
        value[color] = Utils.GetColor(fromPiece) == Color.White ? 1 : 0;
        value[Move.fromPiece] = 0xf & (int)fromPiece;
        value[from] = fromIndex;
        value[to] = toIndex;
    }

    public Move(Piece fromPiece, byte fromIndex, byte toIndex, Piece capturePiece)
        : this(fromPiece, fromIndex, toIndex)
    {
        value[Move.capturePiece] = 0xf & (int)capturePiece;
        value[capture] = capturePiece != Piece.None ? toIndex : (byte)0; ;
    }

    public Move(Piece fromPiece, byte fromIndex, byte toIndex, Piece capturePiece, Piece promotionPiece)
        : this(fromPiece, fromIndex, toIndex)
    {
        value[Move.capturePiece] = 0xf & (int)capturePiece;
        value[capture] = capturePiece != Piece.None ? toIndex : (byte)0; ;
        value[Move.promotionPiece] = 0xf & (int)promotionPiece;
    }

    public Move(Piece fromPiece, byte fromIndex, byte toIndex, Piece capturePiece, byte captureIndex)
        : this(fromPiece, fromIndex, toIndex)
    {
        value[Move.capturePiece] = 0xf & (int)capturePiece;
        value[capture] = capturePiece != Piece.None ? captureIndex : (byte)0; ;
    }

    public Move(Piece fromPiece, byte fromIndex, byte toIndex, Piece capturePiece, byte captureIndex, CastlingRights castle)
        : this(fromPiece, fromIndex, toIndex)
    {
        value[Move.capturePiece] = 0xf & (int)capturePiece;
        value[capture] = capturePiece != Piece.None ? captureIndex : (byte)0;
        value[castling] = (byte)castle;
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
        return left.value.Data == right.value.Data;
    }

    public static bool operator !=(Move left, Move right) => !(left == right);

    public override int GetHashCode() => value.GetHashCode();

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
