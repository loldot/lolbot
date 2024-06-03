
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
    public static ushort[] GetPseudoLegalMove(Piece piece, Square square)
    {
        var index = Utils.IndexFromSquare(square);
        return piece switch
        {
            Piece.WhitePawn => PawnMoves[index],
            Piece.WhiteKnight => KnightMoves[index],
            Piece.WhiteBishop => throw new NotImplementedException(),
            Piece.WhiteRook => throw new NotImplementedException(),
            Piece.WhiteQueen => throw new NotImplementedException(),
            Piece.WhiteKing => throw new NotImplementedException(),
            Piece.BlackPawn => throw new NotFiniteNumberException(),
            Piece.BlackKnight => KnightMoves[index],
            Piece.BlackBishop => throw new NotImplementedException(),
            Piece.BlackRook => throw new NotImplementedException(),
            Piece.BlackQueen => throw new NotImplementedException(),
            Piece.BlackKing => throw new NotImplementedException(),
            _ => []
        };
    }

    private static ushort[] GenerateKnightMoves(byte squareIndex)
    {
        int count = 0;
        var moves = new ushort[8];

        int[] offsets = [-17, -15, -10, -6, 6, 10, 15, 17];

        var packed = (ushort)(squareIndex << 6);
        foreach (var offset in offsets)
        {
            int to = offset + squareIndex;
            if (to < 0 || to > 63) continue;
            if (Distance(squareIndex, (byte)to) > 2) continue;

            moves[count++] = (ushort)(packed | to);
        }

        Array.Resize(ref moves, count);
        return moves;
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