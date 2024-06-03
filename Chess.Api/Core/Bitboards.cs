using System.Buffers.Binary;
using System.Collections;
using System.Numerics;

namespace Lolbot.Core;

///<summary>
///Provide utility methods for working with bitboards.
///</summary>
public static class Bitboards
{
    public static class Masks
    {
        public const ulong A_File = 0x0101010101010101;
        public const ulong Rank_1 = 0xff;

        public static ulong GetFile(Square square) => A_File << BitOperations.Log2(square);
        public static ulong GetFile(byte index) => A_File << index;

        public static ulong GetRank(Square square) => Rank_1 << BitOperations.Log2(square);
        public static ulong GetRank(byte index) => Rank_1 << index;
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
}