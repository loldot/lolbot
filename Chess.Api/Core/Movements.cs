using System.Buffers.Binary;
using System.Numerics;

namespace Lolbot.Core;

public static class MovePatterns
{
    public static ulong[] WhitePawnPushes = new ulong[64];
    public static ulong[] WhitePawnAttacks = new ulong[64];

    public static ulong[] BlackPawnPushes = new ulong[64];
    public static ulong[] BlackPawnAttacks = new ulong[64];

    public static ulong[] Knights = new ulong[64];
    public static ulong[] Bishops = new ulong[64];
    public static ulong[] Rooks = new ulong[64];

    public static ulong[] Kings = new ulong[64];

    static MovePatterns()
    {
        for (byte i = 0; i < 64; i++)
        {
            var square = Squares.FromIndex(i);
            WhitePawnPushes[i] = GetPawnPushes(square);
            WhitePawnAttacks[i] = GetPawnAttacks(square);
            BlackPawnPushes[i] = Bitboards.FlipAlongVertical(WhitePawnPushes[i ^ 56]);
            BlackPawnAttacks[i] = Bitboards.FlipAlongVertical(WhitePawnAttacks[i ^ 56]);
            Knights[i] = GenerateKnightMoves(i);
            Bishops[i] = GenerateBishopMoves(i);
            Rooks[i] = GenerateRookMoves(i);
            Kings[i] = GenerateKingMoves(i);
        }
    }

    private static ulong GenerateKingMoves(byte squareIndex)
    {
        var attacks = 0ul;

        int[] offsets = [
            +7, +8, +9
            -1,/*k*/+1
            -9, -8, -7
        ];

        foreach (var offset in offsets)
        {
            int to = offset + squareIndex;
            if (to < 0 || to > 63) continue;
            if (Distance(squareIndex, (byte)to) > 1) continue;

            attacks |= Squares.FromIndex((byte)to);
        }

        return attacks;
    }

    private static ulong GenerateRookMoves(byte i)
    {
        return (Bitboards.Masks.GetRank(i) | Bitboards.Masks.GetFile(i)) ^ 1ul << i;
    }

    private static ulong GetDiagonal(int sq)
    {
        const ulong maindia = 0x8040201008040201;
        int diag = 8 * (sq & 7) - (sq & 56);
        int nort = -diag & (diag >> 31);
        int sout = diag & (-diag >> 31);
        return (maindia >> sout) << nort;
    }

    private static ulong GetAntiadiagonal(int sq)
    {
        const ulong maindia = 0x0102040810204080;
        int diag = 56 - 8 * (sq & 7) - (sq & 56);
        int nort = -diag & (diag >> 31);
        int sout = diag & (-diag >> 31);
        return (maindia >> sout) << nort;
    }
    private static ulong GenerateBishopMoves(byte sq)
    {
        return (GetDiagonal(sq) | GetAntiadiagonal(sq)) ^ 1ul << sq;
    }

    private static ulong GenerateKnightMoves(byte squareIndex)
    {
        var attacks = 0ul;

        int[] offsets = [-17, -15, -10, -6, 6, 10, 15, 17];

        foreach (var offset in offsets)
        {
            int to = offset + squareIndex;
            if (to < 0 || to > 63) continue;
            if (Distance(squareIndex, (byte)to) > 2) continue;

            attacks |= Squares.FromIndex((byte)to);
        }

        return attacks;
    }

    private static int Distance(byte x, byte y)
    {
        var (sx, sy) = (Squares.FromIndex(x), Squares.FromIndex(y));
        var (r1, r2) = (Squares.GetRank(sx), Squares.GetRank(sy));
        var (f1, f2) = (Squares.GetFile(sx), Squares.GetFile(sy));

        return Math.Max(Math.Abs(r2 - r1), Math.Abs(f2 - f1));
    }

    private static ulong GetPawnAttacks(ulong pawns)
    {
        const ulong notAFileMask = 0xfefefefefefefefe;
        const ulong notHFileMask = 0x7f7f7f7f7f7f7f7f;
        return ((pawns << 7) & notHFileMask)
            | ((pawns << 9) & notAFileMask);
    }
    private static ulong GetPawnPushes(ulong pawns)
    {
        return pawns << 8 | ((pawns & 0xff00) << 16);
    }

    private static ulong OccludedFill(ulong gen, ulong pro, int dir8)
    {
        int r = shift[dir8];
        pro &= avoidWrap[dir8];
        gen |= pro & BitOperations.RotateLeft(gen, r);
        pro &= BitOperations.RotateLeft(pro, r);
        gen |= pro & BitOperations.RotateLeft(gen, 2 * r);
        pro &= BitOperations.RotateLeft(pro, 2 * r);
        gen |= pro & BitOperations.RotateLeft(gen, 4 * r);
        return gen;
    }

    private static ulong ShiftOne(ulong b, int dir8)
    {
        int r = shift[dir8];
        return BitOperations.RotateLeft(b, r) & avoidWrap[dir8];
    }

    private static ulong SlidingAttacks(ulong sliders, ulong empty, int dir8)
    {
        ulong fill = OccludedFill(sliders, empty, dir8);
        return ShiftOne(fill, dir8);
    }

    // positve left, negative right shifts
    private static readonly int[] shift = { 9, 1, -7, -8, -9, -1, 7, 8 };
    private static readonly ulong[] avoidWrap =
    [
        0xfefefefefefefe00,
        0xfefefefefefefefe,
        0x00fefefefefefefe,
        0x00ffffffffffffff,
        0x007f7f7f7f7f7f7f,
        0x7f7f7f7f7f7f7f7f,
        0x7f7f7f7f7f7f7f00,
        0xffffffffffffff00
    ];



    public static ulong RookAttacks(ulong rooks, ulong empty)
    {
        return SlidingAttacks(rooks, empty, 7)
            | SlidingAttacks(rooks, empty, 1)
            | SlidingAttacks(rooks, empty, 3)
            | SlidingAttacks(rooks, empty, 5);

    }
}