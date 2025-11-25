using System.Runtime.CompilerServices;

namespace Lolbot.Core;

public class MoveOrdering
{
    private const int TTMoveScore = 1_000_000_000;
    private const int WinningCaptureBase = 10_000_000;
    private const int KillerMoveScore = 1_000_000;
    private const int LosingCaptureBase = -10_000_000;

    // MVV-LVA (Most Valuable Victim - Least Valuable Attacker)
    private static readonly int[] VictimValue = [0, 100, 320, 330, 500, 900, 0];
    private static readonly int[] AttackerValue = [0, 10, 32, 33, 50, 90, 0];

    private readonly int[][] historyHeuristic;
    private readonly Move[][] killerMoves;

    public MoveOrdering(int[][] historyHeuristic)
    {
        this.historyHeuristic = historyHeuristic;
        this.killerMoves = new Move[128][];
        for (int i = 0; i < killerMoves.Length; i++)
        {
            killerMoves[i] = new Move[2];
        }
    }

    public void UpdateKiller(Move move, int ply)
    {
        if (move.IsQuiet && killerMoves[ply][0] != move)
        {
            killerMoves[ply][1] = killerMoves[ply][0];
            killerMoves[ply][0] = move;
        }
    }

    public void UpdateHistory(Move move, int depth)
    {
        if (move.IsQuiet)
        {
            historyHeuristic[move.FromIndex][move.ToIndex] += depth * depth;
        }
    }

    public void OrderMoves(Span<Move> moves, int count, Move ttMove, int ply)
    {
        Span<int> scores = stackalloc int[count];

        for (int i = 0; i < count; i++)
        {
            scores[i] = ScoreMove(ref moves[i], ttMove, ply);
        }

        // Selection sort - good enough for small arrays
        for (int i = 0; i < count - 1; i++)
        {
            int bestIdx = i;
            for (int j = i + 1; j < count; j++)
            {
                if (scores[j] > scores[bestIdx])
                {
                    bestIdx = j;
                }
            }

            if (bestIdx != i)
            {
                (moves[i], moves[bestIdx]) = (moves[bestIdx], moves[i]);
                (scores[i], scores[bestIdx]) = (scores[bestIdx], scores[i]);
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int ScoreMove(ref Move move, Move ttMove, int ply)
    {
        // TT move gets highest priority
        if (move == ttMove)
        {
            return TTMoveScore;
        }

        // Captures and promotions
        if (!move.IsQuiet)
        {
            return ScoreCapture(ref move);
        }

        // Killer moves
        if (move == killerMoves[ply][0] || move == killerMoves[ply][1])
        {
            return KillerMoveScore;
        }

        // History heuristic
        return historyHeuristic[move.FromIndex][move.ToIndex];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int ScoreCapture(ref Move move)
    {
        int score = 0;

        // Promotions are very valuable
        if (move.PromotionPieceType != PieceType.None)
        {
            score += VictimValue[(int)move.PromotionPieceType] * 10;
        }

        // MVV-LVA for captures
        if (move.CapturePieceType != PieceType.None)
        {
            int mvvLva = VictimValue[(int)move.CapturePieceType] - AttackerValue[(int)move.FromPieceType];
            
            // Winning captures (capturing higher or equal value)
            if (mvvLva >= 0)
            {
                score += WinningCaptureBase + mvvLva;
            }
            else // Losing captures (capturing lower value)
            {
                score += LosingCaptureBase + mvvLva;
            }
        }

        return score;
    }

    public void ClearKillers()
    {
        for (int i = 0; i < killerMoves.Length; i++)
        {
            killerMoves[i][0] = Move.Null;
            killerMoves[i][1] = Move.Null;
        }
    }
}
