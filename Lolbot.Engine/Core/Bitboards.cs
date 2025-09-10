using System.Buffers.Binary;
using System.Collections;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
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
        public const ulong B_File = 0x0202020202020202;
        public const ulong C_File = 0x0404040404040404;
        public const ulong D_File = 0x0808080808080808;
        public const ulong E_File = 0x1010101010101010;
        public const ulong F_File = 0x2020202020202020;
        public const ulong G_File = 0x4040404040404040;
        public const ulong H_File = 0x8080808080808080;
        public const ulong Rank_1 = 0x00000000000000ff;
        public const ulong Rank_2 = 0x000000000000ff00;
        public const ulong Rank_3 = 0x0000000000ff0000;
        public const ulong Rank_4 = 0x00000000ff000000;
        public const ulong Rank_5 = 0x000000ff00000000;
        public const ulong Rank_6 = 0x0000ff0000000000;
        public const ulong Rank_7 = 0x00ff000000000000;
        public const ulong Rank_8 = 0xff00000000000000;

        public const ulong Edges = A_File | H_File | Rank_1 | Rank_8;

        public const ulong Corners = 0x8100000000000081;

        public const ulong MainDiagonal = 0x8040201008040201;
        public const ulong MainAntidiagonal = 0x0102040810204080;
        public const ulong LightSquares = 0x55AA55AA55AA55AA;
        public const ulong DarkSquares = 0xAA55AA55AA55AA55;

        private static readonly ulong[] NeighbourFiles = [
                     B_File,
            A_File | C_File,
            B_File | D_File,
            C_File | E_File,
            D_File | F_File,
            E_File | G_File,
            F_File | H_File,
            G_File
        ];

        public static ulong GetRank(int sq) { return 0xfful << (sq & 56); }
        public static ulong GetFile(int sq) { return 0x0101010101010101ul << (sq & 7); }
        public static ulong GetNeighbourFiles(int sq) => NeighbourFiles[sq & 7];
        public static ulong GetDiagonal(int sq)
        {
            const ulong maindia = 0x8040201008040201;
            int diag = 8 * (sq & 7) - (sq & 56);
            int nort = -diag & (diag >> 31);
            int sout = diag & (-diag >> 31);
            return (maindia >> sout) << nort;
        }

        public static ulong GetAntiadiagonal(int sq)
        {
            const ulong maindia = 0x0102040810204080;
            int diag = 56 - 8 * (sq & 7) - (sq & 56);
            int nort = -diag & (diag >> 31);
            int sout = diag & (-diag >> 31);
            return (maindia >> sout) << nort;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ulong Pext(ref readonly ulong bitboard, ref ulong mask)
    {
        return Bmi2.X64.ParallelBitExtract(bitboard, mask);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ulong Pdep(ulong bitboard, ulong mask)
    {
        return Bmi2.X64.ParallelBitDeposit(bitboard, mask);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int CountOccupied(ulong bitboard)
        => BitOperations.PopCount(bitboard);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong FlipAlongVertical(ulong bitboard)
        => BinaryPrimitives.ReverseEndianness(bitboard);

    public static Square PopSquare(ref ulong board)
    {
        var lsb = 1ul << BitOperations.TrailingZeroCount(board);
        board ^= lsb;
        return lsb;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte PopLsb(ref ulong board)
    {
        byte lsb = (byte)Bmi1.X64.TrailingZeroCount(board);
        board = Bmi1.X64.ResetLowestSetBit(board);
        return lsb;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static byte Msb(ulong board) => (byte)BitOperations.LeadingZeroCount(board);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static byte Lsb(ulong board) => (byte)BitOperations.TrailingZeroCount(board);

    public static ulong Create(params string[] squares)
    {
        ulong board = 0;
        for (int i = 0; i < squares.Length; i++)
        {
            board |= Squares.FromCoordinates(squares[i]);
        }
        return board;
    }

    public static ulong Create(Vector256<ulong> squares)
    {
        var v = Vector128.Xor(squares.GetLower(), squares.GetUpper());
        return Vector64.Xor(v.GetLower(), v.GetUpper()).ToScalar();
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

    public static ulong Create(params byte[] squares)
    {
        ulong board = 0;
        for (int i = 0; i < squares.Length; i++)
        {
            board |= 1ul << squares[i];
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

    public static void Debug(params ulong[] bitboards)
    {
        foreach (var bitboard in bitboards)
            Console.WriteLine(ToDebugString(bitboard));
    }
    public static void Debug(string header, params ulong[] bitboards)
    {
        Console.WriteLine(header);
        foreach (var bitboard in bitboards)
            Console.WriteLine(ToDebugString(bitboard));
    }

    public static string ToDebugString(ulong bitboard)
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