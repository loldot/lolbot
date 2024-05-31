namespace Chess.Api;

public static class Utils
{
    public static char GetFile(Square square)
    {
        char[] files = ['A', 'B', 'C', 'D', 'E', 'F', 'G', 'H'];
        ulong mask = 0xFFul;
        for (int i = 0; i < files.Length; i++)
        {
            if ((mask & square) != 0) return files[i];
            mask <<= 8;
        }
        return '-';
    }

    public static byte GetRank(Square square)
    {
        ulong mask = 0x0101010101010101ul;
        for (byte i = 1; i <= 8; i++)
        {
            if ((mask & square) != 0) return i;
            mask <<= 1;
        }
        return 0;
    }


    // Little-Endian File-Rank Mapping
    public static Square SquareFromCoordinates(ReadOnlySpan<char> coords)
    {
        byte file = (byte)(coords[0] - 'A');
        byte rank = (byte)(coords[1] - '1');

        return 1ul << (file * 8 + rank);
    }

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

    public static int CountBits(ulong v)
    {
        int c; // c accumulates the total bits set in v
        for (c = 0; v != 0; c++)
        {
            v &= v - 1; // clear the least significant bit set
        }
        return c;
    }
}