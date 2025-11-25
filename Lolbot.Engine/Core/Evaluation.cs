using System.Runtime.CompilerServices;

namespace Lolbot.Core;

public static class Evaluation
{
    // Piece values in centipawns
    private const int PawnValue = 100;
    private const int KnightValue = 320;
    private const int BishopValue = 330;
    private const int RookValue = 500;
    private const int QueenValue = 900;

    // Piece-square tables for positional evaluation
    private static readonly int[] PawnTable = 
    [
        0,  0,  0,  0,  0,  0,  0,  0,
        50, 50, 50, 50, 50, 50, 50, 50,
        10, 10, 20, 30, 30, 20, 10, 10,
        5,  5, 10, 25, 25, 10,  5,  5,
        0,  0,  0, 20, 20,  0,  0,  0,
        5, -5,-10,  0,  0,-10, -5,  5,
        5, 10, 10,-20,-20, 10, 10,  5,
        0,  0,  0,  0,  0,  0,  0,  0
    ];

    private static readonly int[] KnightTable = 
    [
        -50,-40,-30,-30,-30,-30,-40,-50,
        -40,-20,  0,  0,  0,  0,-20,-40,
        -30,  0, 10, 15, 15, 10,  0,-30,
        -30,  5, 15, 20, 20, 15,  5,-30,
        -30,  0, 15, 20, 20, 15,  0,-30,
        -30,  5, 10, 15, 15, 10,  5,-30,
        -40,-20,  0,  5,  5,  0,-20,-40,
        -50,-40,-30,-30,-30,-30,-40,-50
    ];

    private static readonly int[] BishopTable = 
    [
        -20,-10,-10,-10,-10,-10,-10,-20,
        -10,  0,  0,  0,  0,  0,  0,-10,
        -10,  0,  5, 10, 10,  5,  0,-10,
        -10,  5,  5, 10, 10,  5,  5,-10,
        -10,  0, 10, 10, 10, 10,  0,-10,
        -10, 10, 10, 10, 10, 10, 10,-10,
        -10,  5,  0,  0,  0,  0,  5,-10,
        -20,-10,-10,-10,-10,-10,-10,-20
    ];

    private static readonly int[] RookTable = 
    [
        0,  0,  0,  0,  0,  0,  0,  0,
        5, 10, 10, 10, 10, 10, 10,  5,
        -5,  0,  0,  0,  0,  0,  0, -5,
        -5,  0,  0,  0,  0,  0,  0, -5,
        -5,  0,  0,  0,  0,  0,  0, -5,
        -5,  0,  0,  0,  0,  0,  0, -5,
        -5,  0,  0,  0,  0,  0,  0, -5,
        0,  0,  0,  5,  5,  0,  0,  0
    ];

    private static readonly int[] QueenTable = 
    [
        -20,-10,-10, -5, -5,-10,-10,-20,
        -10,  0,  0,  0,  0,  0,  0,-10,
        -10,  0,  5,  5,  5,  5,  0,-10,
        -5,  0,  5,  5,  5,  5,  0, -5,
        0,  0,  5,  5,  5,  5,  0, -5,
        -10,  5,  5,  5,  5,  5,  0,-10,
        -10,  0,  5,  0,  0,  0,  0,-10,
        -20,-10,-10, -5, -5,-10,-10,-20
    ];

    private static readonly int[] KingMiddleGameTable = 
    [
        -30,-40,-40,-50,-50,-40,-40,-30,
        -30,-40,-40,-50,-50,-40,-40,-30,
        -30,-40,-40,-50,-50,-40,-40,-30,
        -30,-40,-40,-50,-50,-40,-40,-30,
        -20,-30,-30,-40,-40,-30,-30,-20,
        -10,-20,-20,-20,-20,-20,-20,-10,
        20, 20,  0,  0,  0,  0, 20, 20,
        20, 30, 10,  0,  0, 10, 30, 20
    ];

    private static readonly int[] KingEndGameTable = 
    [
        -50,-40,-30,-20,-20,-30,-40,-50,
        -30,-20,-10,  0,  0,-10,-20,-30,
        -30,-10, 20, 30, 30, 20,-10,-30,
        -30,-10, 30, 40, 40, 30,-10,-30,
        -30,-10, 30, 40, 40, 30,-10,-30,
        -30,-10, 20, 30, 30, 20,-10,-30,
        -30,-30,  0,  0,  0,  0,-30,-30,
        -50,-30,-30,-30,-30,-30,-30,-50
    ];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Evaluate(MutablePosition position)
    {
        int score = 0;
        
        // Material and positional evaluation
        score += EvaluatePieces(position);
        
        // Mobility bonus
        score += EvaluateMobility(position);

        // Return from current player's perspective
        return position.CurrentPlayer == Colors.White ? score : -score;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int EvaluatePieces(MutablePosition position)
    {
        int score = 0;
        bool isEndgame = position.IsEndgame;

        // White pieces
        score += EvaluatePieceType(position.WhitePawns, PawnValue, PawnTable, false);
        score += EvaluatePieceType(position.WhiteKnights, KnightValue, KnightTable, false);
        score += EvaluatePieceType(position.WhiteBishops, BishopValue, BishopTable, false);
        score += EvaluatePieceType(position.WhiteRooks, RookValue, RookTable, false);
        score += EvaluatePieceType(position.WhiteQueens, QueenValue, QueenTable, false);
        score += EvaluateKing(position.WhiteKing, isEndgame, false);

        // Black pieces
        score -= EvaluatePieceType(position.BlackPawns, PawnValue, PawnTable, true);
        score -= EvaluatePieceType(position.BlackKnights, KnightValue, KnightTable, true);
        score -= EvaluatePieceType(position.BlackBishops, BishopValue, BishopTable, true);
        score -= EvaluatePieceType(position.BlackRooks, RookValue, RookTable, true);
        score -= EvaluatePieceType(position.BlackQueens, QueenValue, QueenTable, true);
        score -= EvaluateKing(position.BlackKing, isEndgame, true);

        // Bishop pair bonus
        if (Bitboards.CountOccupied(position.WhiteBishops) >= 2) score += 30;
        if (Bitboards.CountOccupied(position.BlackBishops) >= 2) score -= 30;

        return score;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int EvaluatePieceType(ulong bitboard, int pieceValue, int[] table, bool isBlack)
    {
        int score = 0;
        ulong pieces = bitboard;
        
        while (pieces != 0)
        {
            int square = Bitboards.PopLsb(ref pieces);
            int tableIndex = isBlack ? (square ^ 56) : square; // Flip for black
            score += pieceValue + table[tableIndex];
        }
        
        return score;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int EvaluateKing(ulong king, bool isEndgame, bool isBlack)
    {
        if (king == 0) return 0;
        
        int square = Squares.ToIndex(king);
        int tableIndex = isBlack ? (square ^ 56) : square;
        
        return isEndgame ? KingEndGameTable[tableIndex] : KingMiddleGameTable[tableIndex];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int EvaluateMobility(MutablePosition position)
    {
        // Simple mobility: count legal moves
        Span<Move> moves = stackalloc Move[218];
        int moveCount = MoveGenerator.Legal(position, ref moves);
        
        return moveCount * 2; // Small mobility bonus
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetPieceValue(PieceType pieceType)
    {
        return pieceType switch
        {
            PieceType.Pawn => PawnValue,
            PieceType.Knight => KnightValue,
            PieceType.Bishop => BishopValue,
            PieceType.Rook => RookValue,
            PieceType.Queen => QueenValue,
            _ => 0
        };
    }
}
