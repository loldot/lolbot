
using System.Runtime.CompilerServices;

namespace Lolbot.Core;

public static class Heuristics
{
    public static readonly int[] DoubledPawnPenalties = [0, 0, -17, -29, -200, -300, -300, -300];
    public const int IsolatedPawnPenalty = -15;
    public const int PassedPawnBonus = 27;

    // https://lichess.org/@/ubdip/blog/finding-the-value-of-pieces/PByOBlNB
    // https://lichess.org/@/ubdip/blog/comments-on-piece-values/Ps9kghhO
    public const short PawnValue = 100;
    public const short KnightValue = 316;
    public const short BishopValue = 328;
    public const short RookValue = 493;
    public const short QueenValue = 982;

    private readonly static short[] PieceValues = [0,
        PawnValue,
        KnightValue,
        BishopValue,
        RookValue,
        QueenValue,
        0
    ];
    public const float StartMaterialValue = 4 * KnightValue
        + 4 * BishopValue
        + 4 * RookValue
        + 2 * QueenValue;

    private static readonly int[][] mvvlva = new int[7][];

    static Heuristics()
    {
        for (int i = 0; i < 7; i++)
        {
            mvvlva[i] = new int[7];

            for (int j = 1; i > 0 && j < 7; j++)
            {
                int capture = PieceValues[i];
                int attacker = PieceValues[j];

                mvvlva[i][j] = 10 * capture - attacker;
            }
        }
    }

    public static int MaterialOnly(MutablePosition position)
    {
        int eval = 0;

        for (PieceType p = PieceType.Pawn; p <= PieceType.Queen; p++)
        {
            var white = position[Colors.White, p];
            var black = position[Colors.Black, p];
            
            eval += PieceValues[(byte)p] * Bitboards.CountOccupied(white);
            eval -= PieceValues[(byte)p] * Bitboards.CountOccupied(black);
        }

        var color = position.CurrentPlayer == Colors.White ? 1 : -1;
        return color * eval;
    }

    public static int StaticEvaluation(MutablePosition position, bool debug = false)
    {
        short middle = 0;
        short end = 0;
        short whitePiecesValue = 0;
        short blackPiecesValue = 0;

        for (PieceType p = PieceType.Knight; p <= PieceType.King; p++)
        {
            var white = position[Colors.White, p];
            while (white != 0)
            {
                var sq = Bitboards.PopLsb(ref white);

                whitePiecesValue += PieceValues[(byte)p];
                middle += PieceSquareTables.GetOpeningBonus(p, (byte)(sq ^ 56));
                end += PieceSquareTables.GetEndgameBonus(p, (byte)(sq ^ 56));
            }

            var black = position[Colors.Black, p];
            while (black != 0)
            {
                var sq = Bitboards.PopLsb(ref black);

                blackPiecesValue += PieceValues[(byte)p];
                middle -= PieceSquareTables.GetOpeningBonus(p, sq);
                end -= PieceSquareTables.GetEndgameBonus(p, sq);
            }
        }

        var phase = (StartMaterialValue - (whitePiecesValue + blackPiecesValue))
            / StartMaterialValue;

        int eval = whitePiecesValue - blackPiecesValue;

        var whitePawns = position[Colors.White, PieceType.Pawn];
        while (whitePawns != 0)
        {
            var sq = Bitboards.PopLsb(ref whitePawns);
            eval += PieceValues[(byte)PieceType.Pawn];
            middle += PieceSquareTables.GetOpeningBonus(PieceType.Pawn, (byte)(sq ^ 56));
            end += PieceSquareTables.GetEndgameBonus(PieceType.Pawn, (byte)(sq ^ 56));
        }

        var blackPawns = position[Colors.Black, PieceType.Pawn];
        while (blackPawns != 0)
        {
            var sq = Bitboards.PopLsb(ref blackPawns);
            eval -= PieceValues[(byte)PieceType.Pawn];
            middle -= PieceSquareTables.GetOpeningBonus(PieceType.Pawn, sq);
            end -= PieceSquareTables.GetEndgameBonus(PieceType.Pawn, sq);
        }

        eval += PawnStructure(position.WhitePawns, position.BlackPawns, Colors.White);
        eval -= PawnStructure(position.BlackPawns, position.WhitePawns, Colors.Black);

        eval += Mobility(position, Colors.White);
        eval -= Mobility(position, Colors.Black);

        middle += KingSafety(position, Colors.White);
        middle -= KingSafety(position, Colors.Black);

        var color = position.CurrentPlayer == Colors.White ? 1 : -1;
        if (position.IsCheck) eval -= color * 50;

        eval = (short)float.Lerp(eval + middle, eval + end, phase);

        if (debug)
        {
            Console.WriteLine($"Base eval: {eval}");
            Console.WriteLine($"Middle PST: {middle}");
            Console.WriteLine($"End PST: {end}");
            Console.WriteLine($"Phase: {phase}");
            Console.WriteLine($"Eval: {eval}");
        }

        return color * eval;
    }

    public static int PawnStructure(MutablePosition position, Colors color)
    {
        var eval = 0;
        var opponent = color == Colors.White ? Colors.Black : Colors.White;

        ulong pawns = position[color, PieceType.Pawn];
        ulong oponentPawns = position[opponent, PieceType.Pawn];

        var file = Bitboards.Masks.A_File;
        for (int i = 0; i < 8; i++, file <<= 1)
        {
            var pawnsOnFile = file & pawns;
            if (pawnsOnFile == 0) continue;

            var neighbours = Bitboards.Masks.GetNeighbourFiles(i);
            var opposingPawns = (neighbours | file) & oponentPawns;

            // Doubled pawns
            eval += DoubledPawnPenalties[Bitboards.CountOccupied(pawnsOnFile)];

            // Isolated pawns
            if ((neighbours & pawns) == 0) eval += Bitboards.CountOccupied(pawnsOnFile) * IsolatedPawnPenalty;

            // Passed pawns
            var frontPawn = color == Colors.White
                ? 63 - Bitboards.Msb(pawnsOnFile)
                : Bitboards.Lsb(pawnsOnFile);

            var passedPawnMask = MovePatterns.PassedPawnMasks[1 & (int)color][frontPawn];
            eval += (passedPawnMask & opposingPawns) == 0 ? PassedPawnBonus : 0;
        }

        return eval;
    }

    public static int PawnStructure(ulong pawns, ulong oponentPawns, Colors color)
    {
        var eval = 0;

        var file = Bitboards.Masks.A_File;
        for (int i = 0; i < 8; i++, file <<= 1)
        {
            var pawnsOnFile = file & pawns;
            if (pawnsOnFile == 0) continue;

            var neighbours = Bitboards.Masks.GetNeighbourFiles(i);
            var opposingPawns = (neighbours | file) & oponentPawns;

            // Doubled pawns
            eval += DoubledPawnPenalties[Bitboards.CountOccupied(pawnsOnFile)];

            // // Isolated pawns
            if ((neighbours & pawns) == 0) eval += Bitboards.CountOccupied(pawnsOnFile) * IsolatedPawnPenalty;

            // Passed pawns
            var frontPawn = color == Colors.White
                ? 63 - Bitboards.Msb(pawnsOnFile)
                : Bitboards.Lsb(pawnsOnFile);

            var passedPawnMask = MovePatterns.PassedPawnMasks[1 & (int)color][frontPawn];
            eval += (passedPawnMask & opposingPawns) == 0 ? PassedPawnBonus : 0;
        }

        return eval;
    }

    public static short KingSafety(MutablePosition position, Colors color)
    {
        var occupied = position.Occupied;
        var king = position[color, PieceType.King];
        var friendly = position[color];
        var sq = Squares.ToIndex(king);

        var ra = MovePatterns.RookAttacks(sq, ref occupied) & ~friendly;
        var ba = MovePatterns.BishopAttacks(sq, ref occupied) & ~friendly;

        var opensquares = Bitboards.CountOccupied(ra)
            + Bitboards.CountOccupied(ba);

        return (short)(8 * (3 - opensquares));
    }

    public static int Mobility(MutablePosition position, Colors color)
    {
        int movecount = 0;

        var occ = position.Occupied;
        ulong rooks = position[color, PieceType.Rook] | position[color, PieceType.Queen];
        while (rooks != 0)
        {
            var sq = Bitboards.PopLsb(ref rooks);
            movecount += Bitboards.CountOccupied(MovePatterns.RookAttacks(sq, ref occ));
        }

        ulong bishops = position[color, PieceType.Bishop] | position[color, PieceType.Queen];
        while (bishops != 0)
        {
            var sq = Bitboards.PopLsb(ref bishops);
            movecount += Bitboards.CountOccupied(MovePatterns.BishopAttacks(sq, ref occ));
        }

        return 10 * (int)Math.Sqrt(movecount);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int MVV_LVA(Piece capture, Piece attacker)
        => mvvlva[0xf & (byte)capture][0xf & (byte)attacker];


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int MVV_LVA(PieceType capture, PieceType attacker)
        => mvvlva[(byte)capture][(byte)attacker];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetPieceValue(Piece piece) => PieceValues[0xf & (byte)piece];

}

public static class PieceSquareTables
{
    // https://www.chessprogramming.org/PeSTO%27s_Evaluation_Function

    static readonly short[] mg_pawn_table = {
        0,   0,   0,   0,   0,   0,  0,   0,
        98, 134,  61,  95,  68, 126, 34, -11,
        -6,   7,  26,  31,  65,  56, 25, -20,
        -14,  13,   6,  21,  23,  12, 17, -23,
        -27,  -2,  -5,  12,  17,   6, 10, -25,
        -26,  -4,  -4, -10,   3,   3, 33, -12,
        -35,  -1, -20, -23, -15,  24, 38, -22,
        0,   0,   0,   0,   0,   0,  0,   0,
    };

    static readonly short[] eg_pawn_table = {
        0,   0,   0,   0,   0,   0,   0,   0,
        178, 173, 158, 134, 147, 132, 165, 187,
        94, 100,  85,  67,  56,  53,  82,  84,
        32,  24,  13,   5,  -2,   4,  17,  17,
        13,   9,  -3,  -7,  -7,  -8,   3,  -1,
        4,   7,  -6,   1,   0,  -5,  -1,  -8,
        13,   8,   8,  10,  13,   0,   2,  -7,
        0,   0,   0,   0,   0,   0,   0,   0,
    };

    static readonly short[] mg_knight_table = {
        -167, -89, -34, -49,  61, -97, -15, -107,
        -73, -41,  72,  36,  23,  62,   7,  -17,
        -47,  60,  37,  65,  84, 129,  73,   44,
        -9,  17,  19,  53,  37,  69,  18,   22,
        -13,   4,  16,  13,  28,  19,  21,   -8,
        -23,  -9,  12,  10,  19,  17,  25,  -16,
        -29, -53, -12,  -3,  -1,  18, -14,  -19,
        -105, -21, -58, -33, -17, -28, -19,  -23,
    };

    static readonly short[] eg_knight_table = {
        -58, -38, -13, -28, -31, -27, -63, -99,
        -25,  -8, -25,  -2,  -9, -25, -24, -52,
        -24, -20,  10,   9,  -1,  -9, -19, -41,
        -17,   3,  22,  22,  22,  11,   8, -18,
        -18,  -6,  16,  25,  16,  17,   4, -18,
        -23,  -3,  -1,  15,  10,  -3, -20, -22,
        -42, -20, -10,  -5,  -2, -20, -23, -44,
        -29, -51, -23, -15, -22, -18, -50, -64,
    };

    static readonly short[] mg_bishop_table = {
        -29,   4, -82, -37, -25, -42,   7,  -8,
        -26,  16, -18, -13,  30,  59,  18, -47,
        -16,  37,  43,  40,  35,  50,  37,  -2,
        -4,   5,  19,  50,  37,  37,   7,  -2,
        -6,  13,  13,  26,  34,  12,  10,   4,
        0,  15,  15,  15,  14,  27,  18,  10,
        4,  15,  16,   0,   7,  21,  33,   1,
        -33,  -3, -14, -21, -13, -12, -39, -21,
    };

    static readonly short[] eg_bishop_table = {
        -14, -21, -11,  -8, -7,  -9, -17, -24,
        -8,  -4,   7, -12, -3, -13,  -4, -14,
        2,  -8,   0,  -1, -2,   6,   0,   4,
        -3,   9,  12,   9, 14,  10,   3,   2,
        -6,   3,  13,  19,  7,  10,  -3,  -9,
        -12,  -3,   8,  10, 13,   3,  -7, -15,
        -14, -18,  -7,  -1,  4,  -9, -15, -27,
        -23,  -9, -23,  -5, -9, -16,  -5, -17,
    };

    static readonly short[] mg_rook_table = {
        32,  42,  32,  51, 63,  9,  31,  43,
        27,  32,  58,  62, 80, 67,  26,  44,
        -5,  19,  26,  36, 17, 45,  61,  16,
        -24, -11,   7,  26, 24, 35,  -8, -20,
        -36, -26, -12,  -1,  9, -7,   6, -23,
        -45, -25, -16, -17,  3,  0,  -5, -33,
        -44, -16, -20,  -9, -1, 11,  -6, -71,
        -19, -13,   1,  17, 16,  7, -37, -26,
    };

    static readonly short[] eg_rook_table = {
        13, 10, 18, 15, 12,  12,   8,   5,
        11, 13, 13, 11, -3,   3,   8,   3,
        7,  7,  7,  5,  4,  -3,  -5,  -3,
        4,  3, 13,  1,  2,   1,  -1,   2,
        3,  5,  8,  4, -5,  -6,  -8, -11,
        -4,  0, -5, -1, -7, -12,  -8, -16,
        -6, -6,  0,  2, -9,  -9, -11,  -3,
        -9,  2,  3, -1, -5, -13,   4, -20,
    };

    static readonly short[] mg_queen_table = {
        -28,   0,  29,  12,  59,  44,  43,  45,
        -24, -39,  -5,   1, -16,  57,  28,  54,
        -13, -17,   7,   8,  29,  56,  47,  57,
        -27, -27, -16, -16,  -1,  17,  -2,   1,
        -9, -26,  -9, -10,  -2,  -4,   3,  -3,
        -14,   2, -11,  -2,  -5,   2,  14,   5,
        -35,  -8,  11,   2,   8,  15,  -3,   1,
        -1, -18,  -9,  10, -15, -25, -31, -50,
    };

    static readonly short[] eg_queen_table = {
        -9,  22,  22,  27,  27,  19,  10,  20,
        -17,  20,  32,  41,  58,  25,  30,   0,
        -20,   6,   9,  49,  47,  35,  19,   9,
        3,  22,  24,  45,  57,  40,  57,  36,
        -18,  28,  19,  47,  31,  34,  39,  23,
        -16, -27,  15,   6,   9,  17,  10,   5,
        -22, -23, -30, -16, -16, -23, -36, -32,
        -33, -28, -22, -43,  -5, -32, -20, -41,
    };

    static readonly short[] mg_king_table = {
        -65,  23,  16, -15, -56, -34,   2,  13,
        29,  -1, -20,  -7,  -8,  -4, -38, -29,
        -9,  24,   2, -16, -20,   6,  22, -22,
        -17, -20, -12, -27, -30, -25, -14, -36,
        -49,  -1, -27, -39, -46, -44, -33, -51,
        -14, -14, -22, -46, -44, -30, -15, -27,
        1,   7,  -8, -64, -43, -16,   9,   8,
        -15,  36,  12, -54,   8, -28,  24,  14,
    };

    static readonly short[] eg_king_table = {
        -74, -35, -18, -18, -11,  15,   4, -17,
        -12,  17,  14,  17,  17,  38,  23,  11,
        10,  17,  23,  15,  20,  45,  44,  13,
        -8,  22,  24,  27,  26,  33,  26,   3,
        -18,  -4,  21,  24,  27,  23,   9, -11,
        -19,  -3,  11,  21,  23,  16,   7,  -9,
        -27, -11,   4,  13,  14,   4,  -5, -17,
        -53, -34, -21, -11, -28, -14, -24, -43
    };

    public static short GetOpeningBonus(PieceType piece, byte square)
    {
        return piece switch
        {
            PieceType.Pawn => mg_pawn_table[square],
            PieceType.Knight => mg_knight_table[square],
            PieceType.Bishop => mg_bishop_table[square],
            PieceType.Rook => mg_rook_table[square],
            PieceType.Queen => mg_queen_table[square],
            PieceType.King => mg_king_table[square],
            _ => throw new NotImplementedException()
        };
    }

    public static short GetEndgameBonus(PieceType piece, byte square)
    {
        return piece switch
        {
            PieceType.Pawn => eg_pawn_table[square],
            PieceType.Knight => eg_knight_table[square],
            PieceType.Bishop => eg_bishop_table[square],
            PieceType.Rook => eg_rook_table[square],
            PieceType.Queen => eg_queen_table[square],
            PieceType.King => eg_king_table[square],
            _ => throw new NotImplementedException()
        };
    }
}