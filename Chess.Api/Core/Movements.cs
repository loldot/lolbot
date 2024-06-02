
using System.Numerics;

namespace Chess.Api;

public static class MovePatterns
{
    public static ushort[][] PawnMoves = new ushort[64][];
    public static ushort[][] KnightMoves = new ushort[64][];


    static MovePatterns()
    {
        for (byte i = 0; i < 64; i++)
        {
            PawnMoves[i] = GeneratePawnMoves(Utils.SquareFromIndex(i));
            KnightMoves[i] = GenerateKnightMoves(i);
        }
    }

    private static ushort[] GenerateKnightMoves(byte i)
    {
        ushort packed = (ushort)(i << 6);
        ushort tr(int offset)
        {
            int to = offset + i;
            if (to < 0 || to > 63) return 0;
            if (Distance(i, (byte)to) > 2) return 0;

            return (ushort)(packed & to);
        }

        return [
            tr(-17), tr(-15), tr(-10), tr(-6),
            tr(6), tr(10), tr(15), tr(17)
        ];
    }

    private static int Distance(byte x, byte y)
    {
        var (sx, sy) = (Utils.SquareFromIndex(x), Utils.SquareFromIndex(y));
        var (r1, r2) = (Utils.GetRank(sx), Utils.GetRank(sy));
        var (f1, f2) = (Utils.GetFile(sx), Utils.GetFile(sy));

        return Math.Max(Math.Abs(r2 - r1), Math.Abs(f2 - f1));
    }

    private static ushort[] GeneratePawnMoves(Square from)
    {
        var count = 0;
        var moves = new ushort[218];

        var pushes = PawnPush(from);
        while (pushes != 0)
        {
            var push = Utils.PopLsb(ref pushes);

            moves[count++] = Utils.PackMove(from, push);
        }

        var attacks = PawnAttacks(from);
        while (attacks != 0)
        {
            var attack = Utils.PopLsb(ref attacks);
            moves[count++] = Utils.PackMove(from, attack);
        }

        Array.Resize(ref moves, count);
        return moves;
    }

    public static ulong PawnAttacks(ulong pawns)
    {
        const ulong notAFileMask = 0xfefefefefefefefe;
        const ulong notHFileMask = 0x7f7f7f7f7f7f7f7f;
        return ((pawns << 7) & notHFileMask)
            | ((pawns << 9) & notAFileMask);
    }
    public static ulong PawnPush(ulong pawns)
    {
        return pawns << 8 | ((pawns & 0xff00) << 16);
    }

}