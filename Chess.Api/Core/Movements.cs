namespace Lolbot.Core;

public static class MovePatterns
{
    public static ulong[] PawnPushes = new ulong[64];
    public static ulong[] PawnAttacks = new ulong[64];

    public static ulong[] KnightMoves = new ulong[64];
    public static ulong[] BishopMoves = new ulong[64];
    public static ulong[] RookMoves = new ulong[64];

    static MovePatterns()
    {
        for (byte i = 0; i < 64; i++)
        {
            var square = Squares.FromIndex(i);
            PawnPushes[i] = GetPawnPushes(square);
            PawnAttacks[i] = GetPawnAttacks(square);
            KnightMoves[i] = GenerateKnightMoves(i);
            BishopMoves[i] = GenerateBishopMoves(i);
            RookMoves[i] = GenerateRookMoves(i);
        }
    }

    private static ulong GenerateRookMoves(byte i)
    {
        return (GetRank(i) | GetFile(i)) ^ 1ul << i;
    }

    private static ulong GetRank(int sq) { return 0xfful << (sq & 56); }

    private static ulong GetFile(int sq) { return 0x0101010101010101ul << (sq & 7); }

    static ulong GetDiagonal(int sq)
    {
        const ulong maindia = 0x8040201008040201;
        int diag = 8 * (sq & 7) - (sq & 56);
        int nort = -diag & (diag >> 31);
        int sout = diag & (-diag >> 31);
        return (maindia >> sout) << nort;
    }

    static ulong GetAntiadiagonal(int sq)
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

}