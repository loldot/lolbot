using System.Runtime.CompilerServices;

namespace Lolbot.Core;

public static class Heuristics
{
    public static int[] PieceValues = [0, 100, 300, 325, 500, 900, 999_999];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetPieceValue(Piece piece) => PieceValues[0xf & (byte)piece];

    public static int GetPieceValue(Piece piece, ulong bitboard)
    {
        return Bitboards.CountOccupied(bitboard) * GetPieceValue(piece) + piece switch
        {
            Piece.WhitePawn => GetBonus(WhitePawnSquareBonus, bitboard),
            Piece.WhiteKnight => GetBonus(WhiteKnightSquareBonus, bitboard),
            Piece.WhiteBishop => GetBonus(WhiteBishopSquareBonus, bitboard),
            Piece.WhiteRook => GetBonus(WhiteRookSquareBonus, bitboard),

            Piece.BlackPawn => GetBonus(BlackPawnSquareBonus, bitboard),
            Piece.BlackKnight => GetBonus(BlackKnightSquareBonus, bitboard),
            Piece.BlackBishop => GetBonus(BlackBishopSquareBonus, bitboard),
            Piece.BlackRook => GetBonus(BlackRookSquareBonus, bitboard),
            _ => 0
        };
    }

    private static int GetBonus((ulong, int)[] squarebonus, ulong bitboard)
    {
        int bonus = 0;
        for (int i = 0; i < squarebonus.Length; i++)
        {
            var (mask, b) = squarebonus[i];
            bonus += b * Bitboards.CountOccupied(mask & bitboard);
        }
        return bonus;
    }

    public static (ulong, int)[] WhiteKnightSquareBonus = [
        (Bitboards.Masks.Corners, -10),
        (0x7e424242427e00, +5),
        (0x3c3c3c3c0000, +15)
    ];

     public static (ulong, int)[] BlackKnightSquareBonus = [
        (Bitboards.Masks.Corners, -10),
        (Bitboards.FlipAlongVertical(0x7e424242427e00), +5),
        (Bitboards.FlipAlongVertical(0x3c3c3c3c0000), +15)
    ];

    public static (ulong, int)[] WhiteRookSquareBonus = [
        (Bitboards.Masks.Rank_7, +25),
        (Bitboards.Create(Squares.D1, Squares.E1), +10),
        (Bitboards.Create(Squares.F1), +5),
    ];

    public static (ulong, int)[] BlackRookSquareBonus = [
        (Bitboards.Masks.Rank_2, +25),
        (Bitboards.Create(Squares.D8, Squares.E8), +10),
        (Bitboards.Create(Squares.F8), +5),
    ];

    public static (ulong, int)[] WhiteBishopSquareBonus = [
        (Bitboards.Masks.Edges, -10),
        (0x42000066244200, +5),
        (0x7e3c18180000, +10)
    ];

    public static (ulong, int)[] BlackBishopSquareBonus = [
        (Bitboards.Masks.Edges, -10),
        (Bitboards.FlipAlongVertical(0x42000066244200), +5),
        (Bitboards.FlipAlongVertical(0x7e3c18180000), +10)
    ];

    public static (ulong, int)[] WhitePawnSquareBonus = [
        (0x1800, -20),
        (0x240000, -10),
        (0x420000, -5),
        (0xc32400006600, 10),
        (0x1818000000, 20),
        (Bitboards.Masks.Rank_7, 50)
    ];

    public static (ulong, int)[] BlackPawnSquareBonus = [
        (Bitboards.FlipAlongVertical(0x1800), -20),
        (Bitboards.FlipAlongVertical(0x240000), -10),
        (Bitboards.FlipAlongVertical(0x420000), -5),
        (Bitboards.FlipAlongVertical(0xc32400006600), 10),
        (Bitboards.FlipAlongVertical(0x1818000000), 20),
        (Bitboards.Masks.Rank_2, 50)
    ];
}