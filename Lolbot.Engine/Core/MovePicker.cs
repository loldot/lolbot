using System;

namespace Lolbot.Core;

public ref struct MovePicker
{
    private const int MaxMoves = 218;

    private readonly MutablePosition position;
    private readonly Move ttMove;
    private readonly Move[] killers;
    private readonly int[][] history;
    private readonly int ply;
    private readonly int killerStride;

    private Span<Move> moveBuffer;
    private bool generated;
    private bool ttReturned;
    private int generatedCount;
    private int captureCount;

    public MovePicker(ref Move[] killers, ref int[][] history, ref Span<Move> buffer, MutablePosition position, Move ttMove, int ply)
    {
        this.killers = killers;
        this.history = history;
        this.moveBuffer = buffer;
        this.position = position;
        this.ttMove = ttMove;
        this.ply = ply;
        generated = false;
        ttReturned = false;
        generatedCount = 0;
        captureCount = 0;
        killerStride = killers.Length > 1 ? killers.Length / 2 : killers.Length;
    }

    public int Count => generated ? generatedCount : 0;

    public Move SelectMove(int ordinal)
    {
        if (ordinal == 0 && !ttReturned && !ttMove.IsNull)
        {
            ttReturned = true;
            return ttMove;
        }

        EnsureGenerated();

        int offset = ordinal;
        if (!ttReturned && !ttMove.IsNull)
        {
            offset -= 1;
        }

        if (offset < 0) offset = 0;
        if (offset >= generatedCount) return Move.Null;
        return moveBuffer[offset];
    }

    public Move PickCapture(int ordinal)
    {
        EnsureGenerated();
        if (ordinal < captureCount)
        {
            return moveBuffer[ordinal];
        }
        return Move.Null;
    }

    private void EnsureGenerated()
    {
        if (generated) return;

        Span<Move> generationBuffer = moveBuffer;
        int total = MoveGenerator.Legal(position, ref generationBuffer);

        Span<Move> quietScratch = stackalloc Move[MaxMoves];
        int captureWrite = 0;
        int quietWrite = 0;
        bool skipTt = !ttMove.IsNull && !ttReturned;

        for (int i = 0; i < total; i++)
        {
            var move = generationBuffer[i];
            if (skipTt && move == ttMove) continue;

            if (move.CapturePieceType != PieceType.None)
            {
                moveBuffer[captureWrite++] = move;
            }
            else
            {
                quietScratch[quietWrite++] = move;
            }
        }

        captureCount = captureWrite;

        SortCaptures(moveBuffer[..captureCount]);

        for (int i = 0; i < quietWrite; i++)
        {
            moveBuffer[captureCount + i] = quietScratch[i];
        }

        SortQuiets(moveBuffer[captureCount..(captureCount + quietWrite)]);

        generatedCount = captureCount + quietWrite;
        generated = true;
    }

    private void SortCaptures(Span<Move> captures)
    {
        Span<int> scores = stackalloc int[captures.Length];
        for (int i = 0; i < captures.Length; i++)
        {
            var move = captures[i];
            scores[i] = Heuristics.MVV_LVA(move.CapturePieceType, move.FromPieceType) + Heuristics.GetPieceValue(move.PromotionPieceType);
        }

        for (int i = 1; i < captures.Length; i++)
        {
            var move = captures[i];
            int score = scores[i];
            int j = i - 1;
            while (j >= 0 && scores[j] < score)
            {
                captures[j + 1] = captures[j];
                scores[j + 1] = scores[j];
                j--;
            }
            captures[j + 1] = move;
            scores[j + 1] = score;
        }
    }

    private void SortQuiets(Span<Move> quiets)
    {
        Span<int> scores = stackalloc int[quiets.Length];
        for (int i = 0; i < quiets.Length; i++)
        {
            scores[i] = ScoreQuiet(quiets[i]);
        }

        for (int i = 1; i < quiets.Length; i++)
        {
            var move = quiets[i];
            int score = scores[i];
            int j = i - 1;
            while (j >= 0 && scores[j] < score)
            {
                quiets[j + 1] = quiets[j];
                scores[j + 1] = scores[j];
                j--;
            }
            quiets[j + 1] = move;
            scores[j + 1] = score;
        }
    }

    private int ScoreQuiet(in Move move)
    {
        int score = 0;
        if (GetKiller(0) == move) score += 90_000;
        else if (GetKiller(1) == move) score += 80_000;

        int colorIndex = move.Color & 1;
        int moveIndex = (int)(move.value & 0xfff);
        score += history[colorIndex][moveIndex];
        return score;
    }

    private Move GetKiller(int slot)
    {
        if (killerStride == 0) return Move.Null;
        int index = ply + slot * killerStride;
        if (index < killers.Length)
        {
            return killers[index];
        }
        return Move.Null;
    }
}
