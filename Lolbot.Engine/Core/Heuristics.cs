
using System.Data;
using System.Runtime.CompilerServices;

namespace Lolbot.Core;

public static class Heuristics
{
    private static readonly int[] GamePhaseInterpolation = [
         0,  0,  0,  0,  0,  0,  0,  0,
         4,  8, 16, 20, 24, 28, 32, 36,
        40, 44, 48, 52, 56, 60, 64, 68,
        72, 76, 80, 84, 88, 92, 96, 100, 100
    ];

    // https://lichess.org/@/ubdip/blog/finding-the-value-of-pieces/PByOBlNB
    // https://lichess.org/@/ubdip/blog/comments-on-piece-values/Ps9kghhO
    public static int[] PieceValues = [0, 100, 316, 328, 493, 982, 9_999];

    private static readonly int[][] mmvlva = new int[7][];

    static Heuristics()
    {
        for (int i = 0; i < 7; i++)
        {
            mmvlva[i] = new int[7];

            for (int j = 1; i > 0 && j < 7; j++)
            {
                int capture = PieceValues[i];
                int attacker = PieceValues[j];

                mmvlva[i][j] = 10 * capture - attacker;
            }
        }
    }

    public static int IsolatedPawns(Position position, Color color)
    {
        var eval = 0;
        ulong pawns = position[color, PieceType.Pawn];
        while (pawns != 0)
        {
            var square = Bitboards.PopLsb(ref pawns);
            if ((Bitboards.Masks.GetNeighbourFiles(square) & position[color, PieceType.Pawn]) == 0)
                eval -= 15;
        }
        return eval;
    }

    public static int Mobility(Position position, Color color)
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
        => mmvlva[0xf & (byte)capture][0xf & (byte)attacker];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetPieceValue(Piece piece) => PieceValues[0xf & (byte)piece];

    public static int GetPieceValue(Piece piece, ulong bitboard, ulong occupied)
    {
        var pieceCount = Math.Max(Bitboards.CountOccupied(occupied), 0);
        var phase = GamePhaseInterpolation[pieceCount];

        int eval = 0;
        while (bitboard != 0)
        {
            var sq = Bitboards.PopLsb(ref bitboard);
            var openingBonus = PieceSquareTables.GetOpeningBonus(piece, sq);
            var endgameBonus = PieceSquareTables.GetEndgameBonus(piece, sq);
            eval += GetPieceValue(piece) + (phase * openingBonus + (100 - phase) * endgameBonus) / 100;
        }

        return eval;
    }
}

public static class PieceSquareTables
{
    // https://www.chessprogramming.org/Simplified_Evaluation_Function

    // Pawn piece-square table
    public static readonly int[] PawnTable =
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

    // Knight piece-square table
    public static readonly int[] KnightTable =
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
    public static readonly int[] BishopTable =
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
    public static readonly int[] RookTable =
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
    public static readonly int[] QueenTable =
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
    public static readonly int[] KingTable =
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
        ..PawnTable,
        ..KnightTable,
        ..BishopTable,
        ..RookTable,
        ..QueenTable,
        ..KingTable,
    ];

    public static readonly int[] EndgameTables = [
        ..PawnTable,
        ..KnightTable,
        ..BishopTable,
        ..RookTable,
        ..QueenTable,
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