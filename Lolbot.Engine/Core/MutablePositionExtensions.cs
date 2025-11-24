using System;

namespace Lolbot.Core;

public static class MutablePositionExtensions
{
    private const int MaxMoves = 218;

    public static int SEE(this MutablePosition position, Move move)
    {
        var working = position.Clone();
        var moveCopy = move;
        int initialGain = CaptureGain(move);

        working.Move(ref moveCopy);

        int response = SeeRecursion(working, move.ToIndex);

        return initialGain - response;
    }

    private static int SeeRecursion(MutablePosition position, int targetIndex)
    {
        Span<Move> moves = stackalloc Move[MaxMoves];
        int count = MoveGenerator.Legal(position, ref moves);

        int best = 0;
        bool hasCapture = false;

        for (int i = 0; i < count; i++)
        {
            var candidate = moves[i];
            if (candidate.ToIndex != targetIndex) continue;
            if (candidate.CapturePieceType == PieceType.None && candidate.PromotionPieceType == PieceType.None) continue;

            hasCapture = true;
            var copy = candidate;
            int gain = CaptureGain(candidate);

            position.Move(ref copy);
            int reply = SeeRecursion(position, targetIndex);
            position.Undo(ref copy);

            int score = gain - reply;
            if (score > best)
            {
                best = score;
            }
        }

        if (!hasCapture) return 0;
        return Math.Max(0, best);
    }

    private static int CaptureGain(in Move move)
    {
        int gain = 0;
        if (move.CapturePieceType != PieceType.None)
        {
            gain += Heuristics.GetPieceValue(move.CapturePieceType);
        }

        if (move.PromotionPieceType != PieceType.None)
        {
            gain += Heuristics.GetPieceValue(move.PromotionPieceType) - Heuristics.GetPieceValue(move.FromPieceType);
        }

        return gain;
    }
}
