using System.Buffers.Binary;
using System.Collections;
using System.Numerics;

namespace Chess.Api;

public static class Utils
{
    public const ulong file_a = 0x0101010101010101;
    public const ulong rank_1 = 0xff;

    public static char GetFile(Square square)
    {
        char[] files = ['a', 'b', 'c', 'd', 'e', 'f', 'g', 'h'];
        return files[BitOperations.Log2(square) & 7];
    }

    public static ulong GetFileMask(Square target) => file_a << BitOperations.Log2(target);

    public static byte GetRank(Square square)
        => (byte)(1 + (BitOperations.Log2(square) >> 3));

    // Little-Endian Rank-File Mapping
    public static Square SquareFromCoordinates(ReadOnlySpan<char> coords)
    {
        byte file = (byte)(char.ToLowerInvariant(coords[0]) - 'a');
        byte rank = (byte)(coords[1] - '1');

        return 1ul << (rank * 8 + file);
    }

    public static Square SquareFromIndex(byte index) => 1ul << index;
    public static byte IndexFromSquare(Square square) => (byte)BitOperations.Log2(square);
    public static byte IndexFromCoordinate(string coords)
    {
        byte file = (byte)(char.ToLowerInvariant(coords[0]) - 'a');
        byte rank = (byte)(coords[1] - '1');

        return (byte)(rank * 8 + file);
    }

    public static string? CoordinateFromSquare(Square? square)
        => (square is not null)
            ? $"{GetFile(square.Value)}{GetRank(square.Value)}"
            : null;

    public static string? CoordinateFromIndex(byte index)
        => CoordinateFromSquare(SquareFromIndex(index));

    public static ulong Bitboard(params string[] squares)
    {
        ulong board = 0;
        for (int i = 0; i < squares.Length; i++)
        {
            board |= SquareFromCoordinates(squares[i]);
        }
        return board;
    }

    public static ulong FlipBitboardVertical(ulong bitboard)
    {
        return BinaryPrimitives.ReverseEndianness(bitboard);
    }

    public static ulong Bitboard(params ulong[] squares)
    {
        ulong board = 0;
        for (int i = 0; i < squares.Length; i++)
        {
            board |= squares[i];
        }
        return board;
    }

    public static int CountBits(ulong v) => BitOperations.PopCount(v);

    public static Square PopLsb(ref ulong board)
    {
        var lsb = 1ul << BitOperations.TrailingZeroCount(board);
        board ^= lsb;
        return lsb;
    }

    public static BitArray ToArray(ulong bitboard) => new(BitConverter.GetBytes(bitboard));

    public static ulong FromArray(ulong[] value)
    {
        ulong l = 0;
        for (int i = 0; i < value.Length; i++)
        {
            l |= value[i] << (56 - (i / 8 * 8) + i % 8);
        }

        return l;
    }

    public static Color GetColor(Piece piece) => (Color)((int)piece >> 4);
    public static char PieceName(Piece piece) => piece switch
    {
        Piece.WhitePawn => 'P',
        Piece.WhiteKnight => 'N',
        Piece.WhiteBishop => 'B',
        Piece.WhiteRook => 'R',
        Piece.WhiteQueen => 'Q',
        Piece.WhiteKing => 'K',
        Piece.BlackPawn => 'p',
        Piece.BlackKnight => 'n',
        Piece.BlackBishop => 'b',
        Piece.BlackRook => 'r',
        Piece.BlackQueen => 'q',
        Piece.BlackKing => 'k',
        _ => ' ',
    };

    public static Piece FromName(char name) => name switch
    {
        'P' => Piece.WhitePawn,
        'N' => Piece.WhiteKnight,
        'B' => Piece.WhiteBishop,
        'R' => Piece.WhiteRook,
        'Q' => Piece.WhiteQueen,
        'K' => Piece.WhiteKing,
        'p' => Piece.BlackPawn,
        'n' => Piece.BlackKnight,
        'b' => Piece.BlackBishop,
        'r' => Piece.BlackRook,
        'q' => Piece.BlackQueen,
        'k' => Piece.BlackKing,
        _ => Piece.None,
    };

    internal static ushort PackMove(ulong from, ulong to)
    {
        ushort ret = IndexFromSquare(from);
        ret <<= 6;
        ret |= IndexFromSquare(to);
        return ret;
    }
}