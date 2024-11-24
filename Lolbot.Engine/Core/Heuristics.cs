
using System.Runtime.CompilerServices;

namespace Lolbot.Core;

public static class Heuristics
{
    public static readonly int[] DoubledPawnPenalties = [0, 0, -17, -29, -200, -300];
    public const int IsolatedPawnPenalty = -15;
    public const int PassedPawnBonus = 27;

    private static readonly int[] GamePhaseInterpolation = [
         0,  0,  0,  0,  0,  0,  0,  0,
         4,  8, 16, 20, 24, 28, 32, 36,
        40, 44, 48, 52, 56, 60, 64, 68,
        72, 76, 80, 84, 88, 92, 96, 100, 100
    ];

    // https://lichess.org/@/ubdip/blog/finding-the-value-of-pieces/PByOBlNB
    // https://lichess.org/@/ubdip/blog/comments-on-piece-values/Ps9kghhO

    public const int PawnValue = 100;
    public const int KnightValue = 316;
    public const int BishopValue = 328;
    public const int RookValue = 493;
    public const int QueenValue = 982;

    public static int[] PieceValues = [0, 
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

    public static int PawnStructure(Position position, Colors color)
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

            var passedPawnMask = MovePatterns.PassedPawnMasks[(int)color][frontPawn];
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

            var passedPawnMask = MovePatterns.PassedPawnMasks[(int)color][frontPawn];
            eval += (passedPawnMask & opposingPawns) == 0 ? PassedPawnBonus : 0;
        }

        return eval;
    }

    public static int KingSafety(in Position position, Colors color)
    {
        var occupied = position.Occupied;
        var king = position[color, PieceType.King];
        var friendly = position[color];
        var sq = Squares.ToIndex(king);

        var ra = MovePatterns.RookAttacks(sq, ref occupied) & ~friendly;
        var ba = MovePatterns.BishopAttacks(sq, ref occupied) & ~friendly;

        var opensquares = Bitboards.CountOccupied(ra)
            + Bitboards.CountOccupied(ba);
        
        return 8 * (3 - opensquares);
    }

    public static int Mobility(ref readonly Position position, Colors color)
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

    public static (int, int) GetPieceValue(Piece piece, ulong bitboard)
    {
        int mg = 0, eg = 0;
        while (bitboard != 0)
        {
            var sq = Bitboards.PopLsb(ref bitboard);

            var openingBonus = PieceSquareTables.GetOpeningBonus(piece, sq);
            var endgameBonus = PieceSquareTables.GetEndgameBonus(piece, sq);
            
            mg += openingBonus;
            eg += endgameBonus;// GetPieceValue(piece) + (phase * openingBonus + (100 - phase) * endgameBonus) / 100;
        }

        return (mg, eg);
    }
}

public static class PieceSquareTables
{
    // https://www.chessprogramming.org/Simplified_Evaluation_Function

    // Pawn piece-square table
    public static readonly int[] Pawns =
    [
        0,  0,  0,  0,  0,  0,  0,  0,
        5, 10, 10,-20,-20, 10, 10,  5,
        5, -5,-10,  0,  0,-10, -5,  5,
        0,  0,  0, 20, 20,  0,  0,  0,
        5,  5, 10, 25, 25, 10,  5,  5,
        10, 10, 20, 30, 30, 20, 10, 10,
        50, 50, 50, 50, 50, 50, 50, 50,
        0,  0,  0,  0,  0,  0,  0,  0,
    ];


    public static readonly int[] PawnsEndgame = [
          0,   0,   0,   0,   0,   0,   0,   0,
          0,   0,   0,   0,   0,   0,   0,   0,
          0,   0,   0,   0,   0,   0,   0,   0,
          0,   0,   0,   0,   0,   0,   0,   0,
         10,  10,  10,  10,  10,  10,  10,  10,
         90,  90,  90,  90,  90,  90,  90,  90,
        180, 165, 130, 130, 130, 155, 170, 170,
          0,   0,   0,   0,   0,   0,   0,   0
    ];

    // Knight piece-square table
    public static readonly int[] Knights =
    [
        -50,-40,-30,-30,-30,-30,-40,-50,
        -40,-20,  0,  5,  5,  0,-20,-40,
        -30,  5, 10, 15, 15, 10,  5,-30,
        -30,  0, 15, 20, 20, 15,  0,-30,
        -30,  5, 15, 20, 20, 15,  5,-30,
        -30,  0, 10, 15, 15, 10,  0,-30,
        -40,-20,  0,  0,  0,  0,-20,-40,
        -50,-40,-30,-30,-30,-30,-40,-50,
    ];

    // Bishop piece-square table
    public static readonly int[] Bishops =
    [
        -20,-10,-10,-10,-10,-10,-10,-20,
        -10,  5,  0,  0,  0,  0,  5,-10,
        -10, 10, 10, 10, 10, 10, 10,-10,
        -10,  0, 10, 10, 10, 10,  0,-10,
        -10,  5,  5, 10, 10,  5,  5,-10,
        -10,  0,  5, 10, 10,  5,  0,-10,
        -10,  0,  0,  0,  0,  0,  0,-10,
        -20,-10,-10,-10,-10,-10,-10,-20,
    ];

    // Rook piece-square table
    public static readonly int[] Rooks =
    [
        0,  0,  0,  5,  5,  0,  0,  0,
        -5,  0,  0,  0,  0,  0,  0, -5,
        -5,  0,  0,  0,  0,  0,  0, -5,
        -5,  0,  0,  0,  0,  0,  0, -5,
        -5,  0,  0,  0,  0,  0,  0, -5,
        -5,  0,  0,  0,  0,  0,  0, -5,
        5, 10, 10, 10, 10, 10, 10,  5,
        0,  0,  0,  0,  0,  0,  0,  0,
    ];

    // Queen piece-square table
    public static readonly int[] Queens =
    [
        -20,-10,-10, -5, -5,-10,-10,-20,
        -10,  0,  5,  0,  0,  0,  0,-10,
        -10,  5,  5,  5,  5,  5,  0,-10,
        0,  0,  5,  5,  5,  5,  0, -5,
        -5,  0,  5,  5,  5,  5,  0, -5,
        -10,  0,  5,  5,  5,  5,  0,-10,
        -10,  0,  0,  0,  0,  0,  0,-10,
        -20,-10,-10, -5, -5,-10,-10,-20,
    ];

    // King piece-square table (middlegame)
    public static readonly int[] King =
    [
        20, 30, 10,  0,  0, 30, 10, 20,
        20, 20,  0,  0,  0,  0, 20, 20,
        -10,-20,-20,-20,-20,-20,-20,-10,
        -20,-30,-30,-40,-40,-30,-30,-20,
        -30,-40,-40,-50,-50,-40,-40,-30,
        -30,-40,-40,-50,-50,-40,-40,-30,
        -30,-40,-40,-50,-50,-40,-40,-30,
        -30,-40,-40,-50,-50,-40,-40,-30,
    ];

    public static readonly int[] KingEndgame = [
        -50,-30,-30,-30,-30,-30,-30,-50,
        -30,-30,  0,  0,  0,  0,-30,-30,
        -30,-10, 20, 30, 30, 20,-10,-30,
        -30,-10, 30, 40, 40, 30,-10,-30,
        -30,-10, 30, 40, 40, 30,-10,-30,
        -30,-10, 20, 30, 30, 20,-10,-30,
        -30,-20,-10,  0,  0,-10,-20,-30,
        -50,-40,-30,-20,-20,-30,-40,-50,
    ];

    public static readonly int[] OpeningTables = [
        ..Pawns,
        ..Knights,
        ..Bishops,
        ..Rooks,
        ..Queens,
        ..King,
    ];

    public static readonly int[] EndgameTables = [
        ..PawnsEndgame,
        ..Knights,
        ..Bishops,
        ..Rooks,
        ..Queens,
        ..KingEndgame,
    ];


    public static int GetOpeningBonus(Piece piece, byte square)
    {
        var pieceType = (int)piece & 0xf;
        square = piece <= Piece.WhiteKing ? square : (byte)(square ^ 56);
        return OpeningTables[(pieceType - 1) * 64 + square];
    }

    public static int GetEndgameBonus(Piece piece, byte square)
    {
        var pieceType = (int)piece & 0xf;
        square = piece <= Piece.WhiteKing ? square : (byte)(square ^ 56);
        return EndgameTables[(pieceType - 1) * 64 + square];
    }
}