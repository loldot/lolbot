using System;
using System.Runtime.CompilerServices;

namespace Lolbot.Core;

public static class Heuristics
{
    public const int PawnValue = 100;
    public const int KnightValue = 320;
    public const int BishopValue = 330;
    public const int RookValue = 500;
    public const int QueenValue = 900;

    public const int IsolatedPawnPenalty = -18;
    public const int DoubledPawnPenalty = -12;
    public const int PassedPawnBonus = 40;
    private const int BishopPairBonus = 30;
    private const int PawnShieldBonus = 12;
    private const int CheckPenalty = 150;

    private static readonly int[] PieceValues =
    {
        0,
        PawnValue,
        KnightValue,
        BishopValue,
        RookValue,
        QueenValue,
        0
    };

    public static int StaticEvaluation(MutablePosition position)
    {
        int whiteScore = Material(position, Colors.White);
        int blackScore = Material(position, Colors.Black);

        whiteScore += PawnStructure(position, Colors.White);
        blackScore += PawnStructure(position, Colors.Black);

        whiteScore += KingSafety(position, Colors.White);
        blackScore += KingSafety(position, Colors.Black);

        if (HasBishopPair(position, Colors.White)) whiteScore += BishopPairBonus;
        if (HasBishopPair(position, Colors.Black)) blackScore += BishopPairBonus;

        int evaluation = whiteScore - blackScore;
        int oriented = position.CurrentPlayer == Colors.White ? evaluation : -evaluation;

        if (position.IsCheck) oriented -= CheckPenalty;

        return oriented;
    }

    public static int PawnStructure(MutablePosition position, Colors color)
    {
        ulong pawns = color == Colors.White ? position.WhitePawns : position.BlackPawns;
        ulong enemyPawns = color == Colors.White ? position.BlackPawns : position.WhitePawns;
        if (pawns == 0) return 0;

        int score = 0;
        ulong copy = pawns;
        while (copy != 0)
        {
            int square = Bitboards.PopLsb(ref copy);
            ulong squareMask = 1UL << square;
            ulong fileMask = Bitboards.Masks.GetFile(square);
            ulong neighbours = Bitboards.Masks.GetNeighbourFiles(square);

            // Isolated pawn: no friendly pawn on adjacent files
            if ((pawns & neighbours) == 0)
            {
                score += IsolatedPawnPenalty;
            }

            // Doubled pawns: another pawn on same file
            if (((pawns & fileMask) & ~squareMask) != 0)
            {
                score += DoubledPawnPenalty;
            }

            if (IsPassedPawn(color, square, enemyPawns))
            {
                score += PassedPawnBonus;
            }
        }

        return score;
    }

    public static int GetPieceValue(Piece piece) => GetPieceValue((PieceType)((int)piece & 0xf));

    public static int GetPieceValue(PieceType piece) => PieceValues[(int)piece];

    public static int MVV_LVA(Piece victim, Piece attacker) => MVV_LVA((PieceType)((int)victim & 0xf), (PieceType)((int)attacker & 0xf));

    public static int MVV_LVA(PieceType victim, PieceType attacker)
    {
        int victimValue = GetPieceValue(victim);
        int attackerValue = GetPieceValue(attacker);
        return victimValue * 16 - attackerValue;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int Material(MutablePosition position, Colors color)
    {
        if (color == Colors.White)
        {
            return Bitboards.CountOccupied(position.WhitePawns) * PieceValues[(int)PieceType.Pawn]
                 + Bitboards.CountOccupied(position.WhiteKnights) * PieceValues[(int)PieceType.Knight]
                 + Bitboards.CountOccupied(position.WhiteBishops) * PieceValues[(int)PieceType.Bishop]
                 + Bitboards.CountOccupied(position.WhiteRooks) * PieceValues[(int)PieceType.Rook]
                 + Bitboards.CountOccupied(position.WhiteQueens) * PieceValues[(int)PieceType.Queen];
        }

        return Bitboards.CountOccupied(position.BlackPawns) * PieceValues[(int)PieceType.Pawn]
             + Bitboards.CountOccupied(position.BlackKnights) * PieceValues[(int)PieceType.Knight]
             + Bitboards.CountOccupied(position.BlackBishops) * PieceValues[(int)PieceType.Bishop]
             + Bitboards.CountOccupied(position.BlackRooks) * PieceValues[(int)PieceType.Rook]
             + Bitboards.CountOccupied(position.BlackQueens) * PieceValues[(int)PieceType.Queen];
    }

    private static bool HasBishopPair(MutablePosition position, Colors color)
    {
        ulong bishops = color == Colors.White ? position.WhiteBishops : position.BlackBishops;
        return Bitboards.CountOccupied(bishops) >= 2;
    }

    private static int KingSafety(MutablePosition position, Colors color)
    {
        ulong king = color == Colors.White ? position.WhiteKing : position.BlackKing;
        if (king == 0) return 0;
        int square = Bitboards.Lsb(king);
        ulong pawns = color == Colors.White ? position.WhitePawns : position.BlackPawns;
        ulong mask = KingShieldMask(color, square);
        int shield = Bitboards.CountOccupied(pawns & mask);
        return shield * PawnShieldBonus;
    }

    private static ulong KingShieldMask(Colors color, int square)
    {
        int file = square & 7;
        int rank = square >> 3;
        ulong mask = 0;

        for (int df = -1; df <= 1; df++)
        {
            int targetFile = file + df;
            if (targetFile < 0 || targetFile > 7) continue;

            for (int dr = 1; dr <= 2; dr++)
            {
                int targetRank = color == Colors.White ? rank + dr : rank - dr;
                if (targetRank < 0 || targetRank > 7) continue;
                mask |= 1UL << (targetRank * 8 + targetFile);
            }
        }

        return mask;
    }

    private static bool IsPassedPawn(Colors color, int square, ulong enemyPawns)
    {
        ulong mask = ForwardMask(color, square, 0)
                   | ForwardMask(color, square, -1)
                   | ForwardMask(color, square, 1);
        return (enemyPawns & mask) == 0;
    }

    private static ulong ForwardMask(Colors color, int square, int fileOffset)
    {
        int file = (square & 7) + fileOffset;
        if (file < 0 || file > 7) return 0;
        int rank = square >> 3;
        ulong mask = 0;

        if (color == Colors.White)
        {
            for (int r = rank + 1; r < 8; r++)
            {
                mask |= 1UL << (r * 8 + file);
            }
        }
        else
        {
            for (int r = rank - 1; r >= 0; r--)
            {
                mask |= 1UL << (r * 8 + file);
            }
        }

        return mask;
    }
}
