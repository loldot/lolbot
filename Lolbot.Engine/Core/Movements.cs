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

    public static readonly ulong[] WhitePawnPushes = new ulong[64];
    public static readonly ulong[] WhitePawnAttacks = new ulong[64];

    public static readonly ulong[] BlackPawnPushes = new ulong[64];
    public static readonly ulong[] BlackPawnAttacks = new ulong[64];
    public static readonly ulong[][] PassedPawnMasks = new ulong[2][];

    public static readonly ulong[] Knights = new ulong[64];
    public static readonly ulong[] Bishops = new ulong[64];
    public static readonly ulong[] Rooks = new ulong[64];

    public static readonly ulong[] Kings = new ulong[64];

    public static readonly ulong[][] SquaresBetween = new ulong[64][];

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
            WhitePawnAttacks[i] = CalculateAllPawnAttacksWhite(square);
            Knights[i] = PseudoKnightMoves(i);
            Bishops[i] = PseudoBishopMoves(i);
            Rooks[i] = PseudoRookMoves(i);
            Kings[i] = PseudoKingMoves(i);

            if (i >= 56) PromotionPieces[i] = [Piece.WhiteQueen, Piece.WhiteRook, Piece.WhiteBishop, Piece.WhiteKnight];
            else if (i <= 7) PromotionPieces[i] = [Piece.BlackQueen, Piece.BlackRook, Piece.BlackBishop, Piece.BlackKnight];
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

        PassedPawnMasks[(int)Colors.White & 1] = new ulong[64];
        PassedPawnMasks[(int)Colors.Black & 1] = new ulong[64];

        for (byte i = 8; i < 56; i++)
        {
            ulong pawn = Squares.FromIndex(i);

            ulong white_mask = pawn | WhitePawnAttacks[i];
            ulong black_mask = pawn | BlackPawnAttacks[i];

            PassedPawnMasks[(int)Colors.White & 1][i] = OccludedFill(white_mask, ulong.MaxValue, N);
            PassedPawnMasks[(int)Colors.Black & 1][i] = OccludedFill(black_mask, ulong.MaxValue, S);
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

        Console.WriteLine($"info Pext init took {sw.ElapsedMilliseconds} ms");
        sw.Stop();
    }

    private static ulong GetEdgeFilter(byte i)
    {
        var result = (Bitboards.Masks.Rank_1 | Bitboards.Masks.Rank_8) & ~Bitboards.Masks.GetRank(i);
        result |= (Bitboards.Masks.A_File | Bitboards.Masks.H_File) & ~Bitboards.Masks.GetFile(i);

        return ~result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong BishopAttacks(byte square, ref readonly ulong occupied)
    {
        var index = BishopPextIndex[square]
            + Bitboards.Pext(in occupied, ref BishopPextMask[square]);
        return PextTable[index];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong RookAttacks(byte square, ref readonly ulong occupied)
    {
        var index = RookPextIndex[square]
            + Bitboards.Pext(in occupied, ref RookPextMask[square]);
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
        return (Bitboards.Masks.GetDiagonal(sq) | Bitboards.Masks.GetAntiadiagonal(sq)) ^ 1ul << sq;
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

    public static ulong CalculateAllPawnAttacksWhite(ulong pawns)
    {
        const ulong notAFileMask = 0xfefefefefefefefe;
        const ulong notHFileMask = 0x7f7f7f7f7f7f7f7f;
        return ((pawns << 7) & notHFileMask)
            | ((pawns << 9) & notAFileMask);
    }

    public static ulong CalculateAllPawnAttacksBlack(ulong pawns)
    {
        const ulong notAFileMask = 0xfefefefefefefefe;
        const ulong notHFileMask = 0x7f7f7f7f7f7f7f7f;
        return ((pawns >> 7) & notHFileMask)
            | ((pawns >> 9) & notAFileMask);
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

    public static ulong SlidingAttacks(ulong sliders, ulong empty, int dir8)
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
        var occupied = ~empty;
        return piece switch
        {
            Piece.WhitePawn => CalculateAllPawnAttacksWhite(bitboard) & occupied,
            Piece.BlackPawn => Bitboards.FlipAlongVertical(CalculateAllPawnAttacksWhite(Bitboards.FlipAlongVertical(bitboard))) & occupied,
            Piece.WhiteKnight => Knights[sq],
            Piece.BlackKnight => Knights[sq],
            Piece.WhiteBishop => BishopAttacks(sq, ref occupied),
            Piece.BlackBishop => BishopAttacks(sq, ref occupied),
            Piece.WhiteRook => RookAttacks(sq, ref occupied),
            Piece.WhiteQueen => RookAttacks(sq, ref occupied) | BishopAttacks(sq, ref occupied),
            Piece.BlackRook => RookAttacks(sq, ref occupied),
            Piece.BlackQueen => RookAttacks(sq, ref occupied) | BishopAttacks(sq, ref occupied),
            _ => 0,
        };
    }
}