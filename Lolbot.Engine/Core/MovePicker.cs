namespace Lolbot.Core;

public ref struct MovePicker
{
    private bool isGenerated = false;

    private Move ttMove;
    private readonly Move[] killers;
    private readonly int[] history;
    private Span<Move> moves;
    private MutablePosition position;
    private int ply;
    public int Count;

    public MovePicker(
        ref readonly Move[] killers,
        ref readonly int[] history,
        ref readonly Span<Move> moves,
        MutablePosition position,
        Move ttMove, int ply)
    {
        this.ttMove = ttMove;
        this.ply = ply;
        this.killers = killers;
        this.history = history;
        this.moves = moves;
        this.position = position;
    }


    public Move SelectMove(int k)
    {
        if (k == 0 && !ttMove.IsNull) return ttMove;
        if (!isGenerated)
        {
            Count = MoveGenerator2.Legal(position, ref moves);
            isGenerated = true;
            
            int index;
            if (!ttMove.IsNull && (index = moves.IndexOf(ttMove)) >= 0)
            {
                moves[index] = moves[0];
                moves[0] = ttMove;
            }
        }

        if (k <= 8)
        {
            int bestScore = 0;
            int bestIndex = k;
            for (var i = k; i < Count; i++)
            {
                var score = ScoreMove(moves[i]);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestIndex = i;
                }
            }

            (moves[bestIndex], moves[k]) = (moves[k], moves[bestIndex]);
        }
        return moves[k];
    }

    private readonly int ScoreMove(Move m)
    {
        int score = 0;

        score += 100_000 * Heuristics.GetPieceValue(m.PromotionPiece);
        score += 100_000 * Heuristics.MVV_LVA(m.CapturePieceType, m.FromPieceType);
        score += killers[ply] == m ? 99_999 : 0;
        score += history[m.value & 0xfffu];

        return score;
    }
}