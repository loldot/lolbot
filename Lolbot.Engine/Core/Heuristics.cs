
using System.Runtime.CompilerServices;

namespace Lolbot.Core;

public static class Heuristics
{
    private static readonly int[] GamePhaseInterpolation = [
         0,  0,  0,  0, 0,  0,  0,  0,  
         1,  1,  2,  3, 5,  8, 13, 21, 
        34, 55, 69, 76, 81, 85, 88, 91, 
        93, 94, 95, 96, 97, 98, 99, 99, 100
    ];

    public static int[] PieceValues = [0, 100, 300, 325, 500, 900, 9_999];

    private static readonly int[][] mmvlva = new int[7][];

    public static readonly PieceSquareTables Opening = new PieceSquareTables();
    public static readonly PieceSquareTables EndGame = new PieceSquareTables();


    static Heuristics()
    {
        for (int i = 0; i < 7; i++)
        {
            mmvlva[i] = new int[7];

            for (int j = 1; i > 0 && j < 7; j++)
            {
                int capture = PieceValues[i];
                int attacker = PieceValues[j];

                int val = capture * capture / (1 + attacker);

                mmvlva[i][j] = val;
            }
        }

        #region Opening Piece Square Tables
        Opening.SetBonus(Piece.WhitePawn, [
            (0x1800, -20),
            (0x240000, -10),
            (0x420000, -5),
            (0xc32400006600, 10),
            (0x1818000000, 20),
            (Bitboards.Masks.Rank_7, 50)
        ]);
        Opening.SetBonus(Piece.BlackPawn, [
            (Bitboards.FlipAlongVertical(0x1800), -20),
            (Bitboards.FlipAlongVertical(0x240000), -10),
            (Bitboards.FlipAlongVertical(0x420000), -5),
            (Bitboards.FlipAlongVertical(0xc32400006600), 10),
            (Bitboards.FlipAlongVertical(0x1818000000), 20),
            (Bitboards.Masks.Rank_2, 50)
        ]);

        Opening.SetBonus(Piece.WhiteKnight, [
            (Bitboards.Masks.Corners, -10),
            (0x7e424242427e00, +5),
            (0x3c3c3c3c0000, +15)
        ]);
        Opening.SetBonus(Piece.BlackKnight, [
            (Bitboards.Masks.Corners, -10),
            (Bitboards.FlipAlongVertical(0x7e424242427e00), +5),
            (Bitboards.FlipAlongVertical(0x3c3c3c3c0000), +15)
        ]);

        Opening.SetBonus(Piece.WhiteBishop, [
            (Bitboards.Masks.Edges, -10),
            (0x42000066244200, +5),
            (0x7e3c18180000, +10)
        ]);
        Opening.SetBonus(Piece.BlackBishop, [
            (Bitboards.Masks.Edges, -10),
            (Bitboards.FlipAlongVertical(0x42000066244200), +5),
            (Bitboards.FlipAlongVertical(0x7e3c18180000), +10)
        ]);

        Opening.SetBonus(Piece.WhiteRook, [
            (Bitboards.Masks.Rank_7, +25),
            (Bitboards.Create(Squares.D1, Squares.E1), +10),
            (Bitboards.Create(Squares.F1), +5),
        ]);
        Opening.SetBonus(Piece.BlackRook, [
            (Bitboards.Masks.Rank_2, +25),
            (Bitboards.Create(Squares.D8, Squares.E8), +10),
            (Bitboards.Create(Squares.F8), +5),
        ]);

        Opening.SetBonus(Piece.WhiteQueen, [
            (0x3c3c3c3e0400, +5),
            (0x1800008180000018, -5),
            (0x6681810000818166, -10),
            (Bitboards.Masks.Corners, -20)
        ]);
        Opening.SetBonus(Piece.BlackQueen, [
            (Bitboards.FlipAlongVertical(0x3c3c3c3e0400), +5),
            (Bitboards.FlipAlongVertical(0x1800008180000018), -5),
            (Bitboards.FlipAlongVertical(0x6681810000818166), -10),
            (Bitboards.Masks.Corners, -20)
        ]);

        Opening.SetBonus(Piece.WhiteKing, [
            (0x1818181800000000, -50),
            (0x6666666618000000, -40),
            (0x8181818166000000, -30),
            (0x817e0000, -20),
            (0xc381, 20),
            (0x42, 35)
        ]);
        Opening.SetBonus(Piece.BlackKing, [
            (Bitboards.FlipAlongVertical(0x1818181800000000), -50),
            (Bitboards.FlipAlongVertical(0x6666666618000000), -40),
            (Bitboards.FlipAlongVertical(0x8181818166000000), -30),
            (Bitboards.FlipAlongVertical(0x817e0000), -20),
            (Bitboards.FlipAlongVertical(0xc381), 20),
            (Bitboards.FlipAlongVertical(0x42), 35)
        ]);
        #endregion

        #region Endgame Piece Square Tables
        EndGame.SetBonus(Piece.WhitePawn, [
            (0xc3000000000000, +175),
            (0x3c000000000000, +140),
            (0xc30000000000, +95),
            (0x3c0000000000, +160),
            (0x3ca58100, -5)
        ]);

        EndGame.SetBonus(Piece.BlackPawn, [
            (Bitboards.FlipAlongVertical(0xc3000000000000), +175),
            (Bitboards.FlipAlongVertical(0x3c000000000000), +140),
            (Bitboards.FlipAlongVertical(0xc30000000000), +95),
            (Bitboards.FlipAlongVertical(0x3c0000000000), +160),
            (Bitboards.FlipAlongVertical(0x3ca58100), -5)
        ]);

        EndGame.SetBonus(Piece.WhiteKnight, [
            (Bitboards.Masks.Corners, -10),
            (0x7e424242427e00, +5),
            (0x3c3c3c3c0000, +15)
        ]);
        EndGame.SetBonus(Piece.BlackKnight, [
            (Bitboards.Masks.Corners, -10),
            (Bitboards.FlipAlongVertical(0x7e424242427e00), +5),
            (Bitboards.FlipAlongVertical(0x3c3c3c3c0000), +15)
        ]);

        EndGame.SetBonus(Piece.WhiteBishop, [
            (Bitboards.Masks.Edges, -10),
            (0x42000066244200, +5),
            (0x7e3c18180000, +10)
        ]);
        EndGame.SetBonus(Piece.BlackBishop, [
            (Bitboards.Masks.Edges, -10),
            (Bitboards.FlipAlongVertical(0x42000066244200), +5),
            (Bitboards.FlipAlongVertical(0x7e3c18180000), +10)
        ]);

        EndGame.SetBonus(Piece.WhiteRook, [
            (Bitboards.Masks.Rank_7, +25),
            (Bitboards.Create(Squares.D1, Squares.E1), +10),
            (Bitboards.Create(Squares.F1), +5),
        ]);
        EndGame.SetBonus(Piece.BlackRook, [
            (Bitboards.Masks.Rank_2, +25),
            (Bitboards.Create(Squares.D8, Squares.E8), +10),
            (Bitboards.Create(Squares.F8), +5),
        ]);

        EndGame.SetBonus(Piece.WhiteQueen, [
            (0x3c3c3c3e0400, +5),
            (0x1800008180000018, -5),
            (0x6681810000818166, -10),
            (Bitboards.Masks.Corners, -20)
        ]);
        EndGame.SetBonus(Piece.BlackQueen, [
            (Bitboards.FlipAlongVertical(0x3c3c3c3e0400), +5),
            (Bitboards.FlipAlongVertical(0x1800008180000018), -5),
            (Bitboards.FlipAlongVertical(0x6681810000818166), -10),
            (Bitboards.Masks.Corners, -20)
        ]);

        EndGame.SetBonus(Piece.WhiteKing, [
            (Bitboards.Masks.Corners, -50),
            (0x81818181817e, -25),
            (0x42427e00, -5),
            (0x7e7e3c3c3c0000, +20)
        ]);

        EndGame.SetBonus(Piece.BlackKing, [
            (Bitboards.Masks.Corners, -50),
            (Bitboards.FlipAlongVertical(0x81818181817e), -25),
            (Bitboards.FlipAlongVertical(0x42427e00), -5),
            (Bitboards.FlipAlongVertical(0x7e7e3c3c3c0000), +20)
        ]);

        #endregion
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

        return (int)Math.Sqrt(movecount);
    }

    public static int MVV_LVA(Piece capture, Piece attacker)
        => mmvlva[0xf & (byte)capture][0xf & (byte)attacker];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetPieceValue(Piece piece) => PieceValues[0xf & (byte)piece];

    public static int GetPieceValue(Piece piece, ulong bitboard, ulong occupied)
    {
        var pieceCount = Math.Max(Bitboards.CountOccupied(occupied), 0);
        var phase = GamePhaseInterpolation[pieceCount];

        var openingBonus = Opening.GetBonus(piece, bitboard);
        var endgameBonus = EndGame.GetBonus(piece, bitboard);

        var bonus = (phase * openingBonus + (100 - phase) * endgameBonus) / 100;

        return Bitboards.CountOccupied(bitboard) * GetPieceValue(piece) + bonus;
    }
}

public sealed class PieceSquareTables
{
    (ulong bitboard, int bonus)[][] tables = new (ulong, int)[0x27][];

    public void SetBonus(Piece piece, (ulong, int)[] squarebonus)
    {
        tables[(int)piece] = squarebonus;
    }

    public int GetBonus(Piece piece, ulong bitboard)
    {
        int bonus = 0;
        var table = tables[(int)piece];

        for (int i = 0; i < table.Length; i++)
        {
            var (mask, b) = table[i];
            bonus += b * Bitboards.CountOccupied(mask & bitboard);
        }
        return bonus;
    }
}