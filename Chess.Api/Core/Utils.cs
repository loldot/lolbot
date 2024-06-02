using System.Collections;
using System.Collections.Specialized;
using System.Numerics;

namespace Chess.Api;

public static class Utils
{
    public static char GetFile(Square square)
    {
        char[] files = ['a', 'b', 'c', 'd', 'e', 'f', 'g', 'h'];
        return files[BitOperations.Log2(square) & 7];
    }

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

    public static ulong Bitboard(params ulong[] squares)
    {
        ulong board = 0;
        for (int i = 0; i < squares.Length; i++)
        {
            board |= squares[i];
        }
        return board;
    }

    public static BitArray ToArray(ulong bitboard) => new(BitConverter.GetBytes(bitboard));
    public static int CountBits(ulong v) => BitOperations.PopCount(v);

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
}