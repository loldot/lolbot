using System.Runtime.CompilerServices;

namespace Lolbot.Core;

public static class Evaluation
{
    public const int Mate = 30000;
    public const int MateThreshold = Mate - 1000;

    // Piece values in centipawns
    private static readonly int[] PieceValues = [0, 100, 320, 330, 500, 900, 0];

    // Piece-square tables (from white's perspective)
    // Pawn PST
    private static readonly int[] PawnTable = [
        0,   0,   0,   0,   0,   0,   0,   0,
        50,  50,  50,  50,  50,  50,  50,  50,
        10,  10,  20,  30,  30,  20,  10,  10,
        5,   5,  10,  25,  25,  10,   5,   5,
        0,   0,   0,  20,  20,   0,   0,   0,
        5,  -5, -10,   0,   0, -10,  -5,   5,
        5,  10,  10, -20, -20,  10,  10,   5,
        0,   0,   0,   0,   0,   0,   0,   0
    ];

    // Knight PST
    private static readonly int[] KnightTable = [
        -50, -40, -30, -30, -30, -30, -40, -50,
        -40, -20,   0,   0,   0,   0, -20, -40,
        -30,   0,  10,  15,  15,  10,   0, -30,
        -30,   5,  15,  20,  20,  15,   5, -30,
        -30,   0,  15,  20,  20,  15,   0, -30,
        -30,   5,  10,  15,  15,  10,   5, -30,
        -40, -20,   0,   5,   5,   0, -20, -40,
        -50, -40, -30, -30, -30, -30, -40, -50
    ];

    // Bishop PST
    private static readonly int[] BishopTable = [
        -20, -10, -10, -10, -10, -10, -10, -20,
        -10,   0,   0,   0,   0,   0,   0, -10,
        -10,   0,   5,  10,  10,   5,   0, -10,
        -10,   5,   5,  10,  10,   5,   5, -10,
        -10,   0,  10,  10,  10,  10,   0, -10,
        -10,  10,  10,  10,  10,  10,  10, -10,
        -10,   5,   0,   0,   0,   0,   5, -10,
        -20, -10, -10, -10, -10, -10, -10, -20
    ];

    // Rook PST
    private static readonly int[] RookTable = [
        0,   0,   0,   0,   0,   0,   0,   0,
        5,  10,  10,  10,  10,  10,  10,   5,
        -5,   0,   0,   0,   0,   0,   0,  -5,
        -5,   0,   0,   0,   0,   0,   0,  -5,
        -5,   0,   0,   0,   0,   0,   0,  -5,
        -5,   0,   0,   0,   0,   0,   0,  -5,
        -5,   0,   0,   0,   0,   0,   0,  -5,
        0,   0,   0,   5,   5,   0,   0,   0
    ];

    // Queen PST
    private static readonly int[] QueenTable = [
        -20, -10, -10,  -5,  -5, -10, -10, -20,
        -10,   0,   0,   0,   0,   0,   0, -10,
        -10,   0,   5,   5,   5,   5,   0, -10,
        -5,   0,   5,   5,   5,   5,   0,  -5,
        0,   0,   5,   5,   5,   5,   0,  -5,
        -10,   5,   5,   5,   5,   5,   0, -10,
        -10,   0,   5,   0,   0,   0,   0, -10,
        -20, -10, -10,  -5,  -5, -10, -10, -20
    ];

    // King middle game PST
    private static readonly int[] KingMiddleGameTable = [
        -30, -40, -40, -50, -50, -40, -40, -30,
        -30, -40, -40, -50, -50, -40, -40, -30,
        -30, -40, -40, -50, -50, -40, -40, -30,
        -30, -40, -40, -50, -50, -40, -40, -30,
        -20, -30, -30, -40, -40, -30, -30, -20,
        -10, -20, -20, -20, -20, -20, -20, -10,
        20,  20,   0,   0,   0,   0,  20,  20,
        20,  30,  10,   0,   0,  10,  30,  20
    ];

    // King endgame PST
    private static readonly int[] KingEndGameTable = [
        -50, -40, -30, -20, -20, -30, -40, -50,
        -30, -20, -10,   0,   0, -10, -20, -30,
        -30, -10,  20,  30,  30,  20, -10, -30,
        -30, -10,  30,  40,  40,  30, -10, -30,
        -30, -10,  30,  40,  40,  30, -10, -30,
        -30, -10,  20,  30,  30,  20, -10, -30,
        -30, -30,   0,   0,   0,   0, -30, -30,
        -50, -30, -30, -30, -30, -30, -30, -50
    ];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int FlipSquare(int square) => square ^ 56;

    public static int Evaluate(MutablePosition position)
    {
        int score = 0;
        bool isEndgame = position.IsEndgame;

        // Material and positional evaluation for white pieces
        score += EvaluatePieces(position.WhitePawns, PieceType.Pawn, false, isEndgame);
        score += EvaluatePieces(position.WhiteKnights, PieceType.Knight, false, isEndgame);
        score += EvaluatePieces(position.WhiteBishops, PieceType.Bishop, false, isEndgame);
        score += EvaluatePieces(position.WhiteRooks, PieceType.Rook, false, isEndgame);
        score += EvaluatePieces(position.WhiteQueens, PieceType.Queen, false, isEndgame);
        score += EvaluatePieces(position.WhiteKing, PieceType.King, false, isEndgame);

        // Material and positional evaluation for black pieces
        score -= EvaluatePieces(position.BlackPawns, PieceType.Pawn, true, isEndgame);
        score -= EvaluatePieces(position.BlackKnights, PieceType.Knight, true, isEndgame);
        score -= EvaluatePieces(position.BlackBishops, PieceType.Bishop, true, isEndgame);
        score -= EvaluatePieces(position.BlackRooks, PieceType.Rook, true, isEndgame);
        score -= EvaluatePieces(position.BlackQueens, PieceType.Queen, true, isEndgame);
        score -= EvaluatePieces(position.BlackKing, PieceType.King, true, isEndgame);

        // Bishop pair bonus
        if (Bitboards.CountOccupied(position.WhiteBishops) >= 2) score += 30;
        if (Bitboards.CountOccupied(position.BlackBishops) >= 2) score -= 30;

        // Return score from current player's perspective
        return position.CurrentPlayer == Colors.White ? score : -score;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int EvaluatePieces(ulong pieces, PieceType pieceType, bool isBlack, bool isEndgame)
    {
        int score = 0;
        ulong bb = pieces;

        while (bb != 0)
        {
            byte square = Bitboards.PopLsb(ref bb);
            score += PieceValues[(int)pieceType];
            score += GetPieceSquareValue(pieceType, square, isBlack, isEndgame);
        }

        return score;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int GetPieceSquareValue(PieceType pieceType, byte square, bool isBlack, bool isEndgame)
    {
        int index = isBlack ? FlipSquare(square) : square;

        return pieceType switch
        {
            PieceType.Pawn => PawnTable[index],
            PieceType.Knight => KnightTable[index],
            PieceType.Bishop => BishopTable[index],
            PieceType.Rook => RookTable[index],
            PieceType.Queen => QueenTable[index],
            PieceType.King => isEndgame ? KingEndGameTable[index] : KingMiddleGameTable[index],
            _ => 0
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int MateIn(int ply) => Mate - ply;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int MatedIn(int ply) => -Mate + ply;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsMateScore(int score) => Math.Abs(score) > MateThreshold;
}
