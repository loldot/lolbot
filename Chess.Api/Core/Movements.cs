using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace Lolbot.Core;

public static class MovePatterns
{
    public const int NW = 7, N = 8, NE = 9;
    public const int W = -1, O = 0, E = 1;
    public const int SW = -9, S = -8, SE = -7;
    public static readonly int[] Directions = [NW, N, NE, W, E, SW, S, SE];

    public static ulong[] WhitePawnPushes = new ulong[64];
    public static ulong[] WhitePawnAttacks = new ulong[64];

    public static ulong[] BlackPawnPushes = new ulong[64];
    public static ulong[] BlackPawnAttacks = new ulong[64];

    public static ulong[] Knights = new ulong[64];
    public static ulong[] Bishops = new ulong[64];
    public static ulong[] Rooks = new ulong[64];

    public static ulong[] Kings = new ulong[64];

    public static ulong[][] SquaresBetween = new ulong[64][];

    public static Piece[][] PromotionPieces = new Piece[64][];

    public static ulong[] RookPextMask = new ulong[64];
    public static ulong[] BishopPextMask = new ulong[64];
    public static uint[] RookPextIndex = new uint[64];
    public static uint[] BishopPextIndex = new uint[64];

    public static ulong[] PextTable = new ulong[107_648];

    static MovePatterns()
    {
        for (byte i = 0; i < 64; i++)
        {
            var square = Squares.FromIndex(i);
            WhitePawnPushes[i] = GetPawnPushes(square);
            WhitePawnAttacks[i] = CalculateAllPawnAttacks(square);
            Knights[i] = PseudoKnightMoves(i);
            Bishops[i] = PseudoBishopMoves(i);
            Rooks[i] = PseudoRookMoves(i);
            Kings[i] = PseudoKingMoves(i);

            if (i >= 56) PromotionPieces[i] = [Piece.WhiteQueen, Piece.WhiteRook, Piece.WhiteBishop, Piece.WhiteKnight];
            else if (i <= 8) PromotionPieces[i] = [Piece.BlackQueen, Piece.BlackRook, Piece.BlackBishop, Piece.BlackKnight];
            else PromotionPieces[i] = [Piece.None];
        }

        // need to run after generating all squares for white 
        for (byte i = 0; i < 64; i++)
        {
            BlackPawnPushes[i] = Bitboards.FlipAlongVertical(WhitePawnPushes[i ^ 56]);
            BlackPawnAttacks[i] = Bitboards.FlipAlongVertical(WhitePawnAttacks[i ^ 56]);

            var origin = Squares.FromIndex(i);
            SquaresBetween[i] = new ulong[64];
            for (byte j = 0; j < 64; j++)
            {
                var target = Squares.FromIndex(j);
                foreach (var dir in Directions)
                {
                    var ray = SlidingAttacks(origin, ~target, dir);
                    if ((ray & target) != 0)
                    {
                        SquaresBetween[i][j] |= ray;
                    }
                }

                SquaresBetween[i][j] |= target;
            }
        }

        InitPextTable();
    }

    public static void InitPextTable()
    {
        var sw = Stopwatch.StartNew();
        uint currentIndex = 0;
        for (byte i = 0; i < 64; i++)
        {
            var sq = Squares.FromIndex(i);

            RookPextIndex[i] = currentIndex;
            RookPextMask[i] = Rooks[i] & GetEdgeFilter(i);

            var mask = RookPextMask[i];

            ulong max = 1UL << Bitboards.CountOccupied(mask);
            for (ulong j = 0; j < max; j++)
            {
                var blockers = Bitboards.Pepd(j, mask);
                PextTable[currentIndex++] = GenerateRookAttacks(sq, ~blockers);
            }
        }

        for (byte i = 0; i < 64; i++)
        {
            var sq = Squares.FromIndex(i);

            BishopPextIndex[i] = currentIndex;
            BishopPextMask[i] = Bishops[i] & GetEdgeFilter(i);

            var mask = BishopPextMask[i];
            ulong max = 1UL << Bitboards.CountOccupied(mask);
            for (ulong j = 0; j < max; j++)
            {
                var blockers = Bitboards.Pepd(j, mask);
                PextTable[currentIndex++] = GenerateBishopAttacks(sq, ~blockers);
            }
        }

        Console.WriteLine(currentIndex);
        Console.WriteLine($"Pext init took {sw.ElapsedMilliseconds} ms");
        sw.Stop();
    }

    private static ulong GetEdgeFilter(byte i)
    {
        var result = (Bitboards.Masks.Rank_1 | Bitboards.Masks.Rank_8) & ~Bitboards.Masks.GetRank(i);
        result |= (Bitboards.Masks.A_File | Bitboards.Masks.H_File) & ~Bitboards.Masks.GetFile(i);

        return ~result;
    }

    public static ulong BishopAttacks(byte square, ulong occupied)
    {
        var index = BishopPextIndex[square]
            + Bitboards.Pext(occupied, BishopPextMask[square]);
        return PextTable[index];
    }

    public static ulong RookAttacks(byte square, ulong occupied)
    {
        var index = RookPextIndex[square]
            + Bitboards.Pext(occupied, RookPextMask[square]);
        return PextTable[index];
    }

    private static ulong PseudoKingMoves(byte squareIndex)
    {
        var attacks = 0ul;

        int[] offsets = [
            NW, N, NE,
            W,/*k*/ E,
            SW, S, SE
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

    private static ulong PseudoRookMoves(byte i)
    {
        return (Bitboards.Masks.GetRank(i) | Bitboards.Masks.GetFile(i)) ^ 1ul << i;
    }

    private static ulong PseudoBishopMoves(byte sq)
    {
        return (GetDiagonal(sq) | GetAntiadiagonal(sq)) ^ 1ul << sq;
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


    private static ulong PseudoKnightMoves(byte squareIndex)
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

    public static ulong GenerateBishopAttacks(ulong bishops, ulong empty)
    {
        return SlidingAttacks(bishops, empty, NE)
            | SlidingAttacks(bishops, empty, SE)
            | SlidingAttacks(bishops, empty, SW)
            | SlidingAttacks(bishops, empty, NW);
    }

    public static ulong GenerateRookAttacks(ulong rooks, ulong empty)
    {
        return SlidingAttacks(rooks, empty, N)
            | SlidingAttacks(rooks, empty, E)
            | SlidingAttacks(rooks, empty, S)
            | SlidingAttacks(rooks, empty, W);

    }
    private static int Distance(byte x, byte y)
    {
        var (sx, sy) = (Squares.FromIndex(x), Squares.FromIndex(y));
        var (r1, r2) = (Squares.GetRank(sx), Squares.GetRank(sy));
        var (f1, f2) = (Squares.GetFile(sx), Squares.GetFile(sy));

        return Math.Max(Math.Abs(r2 - r1), Math.Abs(f2 - f1));
    }

    public static ulong CalculateAllPawnAttacks(ulong pawns)
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

    private static ulong OccludedFill(ulong gen, ulong pro, int direction)
    {
        int r = direction;
        pro &= AvoidWrap(direction);
        gen |= pro & BitOperations.RotateLeft(gen, r);
        pro &= BitOperations.RotateLeft(pro, r);
        gen |= pro & BitOperations.RotateLeft(gen, 2 * r);
        pro &= BitOperations.RotateLeft(pro, 2 * r);
        gen |= pro & BitOperations.RotateLeft(gen, 4 * r);
        return gen;
    }

    private static ulong ShiftOne(ulong b, int dir8)
    {
        return BitOperations.RotateLeft(b, dir8) & AvoidWrap(dir8);
    }

    private static ulong SlidingAttacks(ulong sliders, ulong empty, int dir8)
    {
        ulong fill = OccludedFill(sliders, empty, dir8);
        return ShiftOne(fill, dir8);
    }

    private static ulong AvoidWrap(int direction)
    {
        return direction switch
        {
            NE => 0xfefefefefefefe00,
            E => 0xfefefefefefefefe,
            SE => 0x00fefefefefefefe,
            S => 0x00ffffffffffffff,
            SW => 0x007f7f7f7f7f7f7f,
            W => 0x7f7f7f7f7f7f7f7f,
            NW => 0x7f7f7f7f7f7f7f00,
            N => 0xffffffffffffff00,
            _ => 0
        };
    }

    internal static ulong GetAttack(Piece piece, ulong bitboard, ulong empty)
    {
        var sq = Squares.ToIndex(bitboard);
        return piece switch
        {
            Piece.WhitePawn => CalculateAllPawnAttacks(bitboard) & ~empty,
            Piece.BlackPawn => Bitboards.FlipAlongVertical(CalculateAllPawnAttacks(Bitboards.FlipAlongVertical(bitboard))) & ~empty,
            Piece.WhiteKnight => Knights[sq],
            Piece.BlackKnight => Knights[sq],
            Piece.WhiteBishop => BishopAttacks(sq, ~empty),
            Piece.BlackBishop => BishopAttacks(sq, ~empty),
            Piece.WhiteRook => RookAttacks(sq, ~empty),
            Piece.WhiteQueen => RookAttacks(sq, ~empty) | BishopAttacks(sq, ~empty),
            Piece.BlackRook => RookAttacks(sq, ~empty),
            Piece.BlackQueen => RookAttacks(sq, ~empty) | BishopAttacks(sq, ~empty),
            _ => 0,
        };
    }
}