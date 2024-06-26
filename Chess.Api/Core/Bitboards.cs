using System.Buffers.Binary;
using System.Collections;
using System.Numerics;
using System.Text;

namespace Lolbot.Core;

///<summary>
///Provide utility methods for working with bitboards.
///</summary>
public static class Bitboards
{
    public static class Masks
    {
        public const ulong A_File = 0x0101010101010101;
        public const ulong H_File = 0x8080808080808080;
        public const ulong Rank_1 = 0xff;
        public const ulong Rank_8 = 0xFF00000000000000;

        public const ulong MainDiagonal = 0x8040201008040201;
        public const ulong MainAntidiagonal = 0x0102040810204080;
        public const ulong LightSquares = 0x55AA55AA55AA55AA;
        public const ulong DarkSquares = 0xAA55AA55AA55AA55;

        public static ulong GetRank(int sq) { return 0xfful << (sq & 56); }
        public static ulong GetFile(int sq) { return 0x0101010101010101ul << (sq & 7); }
    }

    public static int CountOccupied(ulong bitboard)
        => BitOperations.PopCount(bitboard);

    public static ulong FlipAlongVertical(ulong bitboard)
        => BinaryPrimitives.ReverseEndianness(bitboard);

    public static Square PopSquare(ref ulong board)
    {
        var lsb = 1ul << BitOperations.TrailingZeroCount(board);
        board ^= lsb;
        return lsb;
    }

    public static byte PopLsb(ref ulong board)
    {
        var lsb = (byte)BitOperations.TrailingZeroCount(board);
        board ^= Squares.FromIndex(lsb);
        return lsb;
    }

    public static ulong Create(params string[] squares)
    {
        ulong board = 0;
        for (int i = 0; i < squares.Length; i++)
        {
            board |= Squares.FromCoordinates(squares[i]);
        }
        return board;
    }

    public static ulong Create(params ulong[] squares)
    {
        ulong board = 0;
        for (int i = 0; i < squares.Length; i++)
        {
            board |= squares[i];
        }
        return board;
    }
    public static ulong Create(int[] value)
    {
        ulong l = 0;
        for (int i = 0; i < value.Length; i++)
        {
            l |= (ulong)value[i] << (56 - (i / 8 * 8) + i % 8);
        }

        return l;
    }

    public static string[] ToCoordinates(ulong board)
    {
        var squares = new List<string>();

        while (board != 0)
        {
            var square = PopSquare(ref board);

            squares.Add(Squares.ToCoordinate(square)!);
        }

        return [.. squares];
    }

    public static BitArray ToArray(ulong bitboard) => new(BitConverter.GetBytes(bitboard));

    public static void Debug(ulong bitboard)
    {
        Console.Write(ToDebugString(bitboard));
    }

    private static string ToDebugString(ulong bitboard)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"val: 0x{bitboard:X}");
        sb.AppendLine($"pop: {CountOccupied(bitboard)}");

        sb.AppendLine("+-+-+-+-+-+-+-+-+");
        for (char rank = '8'; rank > '0'; rank--)
        {
            sb.Append('|');
            for (char file = 'a'; file <= 'h'; file++)
            {
                var sq = Squares.FromCoordinates("" + file + rank);
                var c = ((bitboard & sq) != 0) ? "*|" : " |";
                sb.Append(c);
            }
            sb.AppendLine($"{rank}");

        }

        sb.AppendLine("+-+-+-+-+-+-+-+-+");
        sb.AppendLine("|a|b|c|d|e|f|g|h|");
        return sb.ToString();
    }
}