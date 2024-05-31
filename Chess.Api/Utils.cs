namespace Chess.Api;

public static class Utils
{
    public static char GetFile(Square square)
    {
        char[] files = ['a', 'b', 'c', 'd', 'e', 'f', 'g', 'h'];
        ulong mask = 0x0101010101010101ul;
        for (int i = 0; i < files.Length; i++)
        {
            if ((mask & square) != 0) return files[i];
            mask <<= 1;
        }
        return '-';
    }

    public static byte GetRank(Square square)
    {
        ulong mask = 0xff;
        for (byte i = 1; i <= 8; i++)
        {
            if ((mask & square) != 0) return i;
            mask <<= 8;
        }
        return 0;
    }


    // Little-Endian Rank-File Mapping
    public static Square SquareFromCoordinates(ReadOnlySpan<char> coords)
    {
        byte file = (byte)(char.ToLowerInvariant(coords[0]) - 'a');
        byte rank = (byte)(coords[1] - '1');

        return 1ul << (rank * 8 + file);
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