namespace Lolbot.Core;

public class RepetitionTable
{
    private int moveCount = 0;

    private int[] irreversible = new int[256];
    private ulong[] history = new ulong[256];

    public void Update(Move m, ulong key)
    {
        if (moveCount >= history.Length)
        {
            Array.Resize(ref history, moveCount * 2);
            Array.Resize(ref irreversible, moveCount * 2);
        }

        history[moveCount] = key;

        if (IsIrreversible(ref m))
        {
            irreversible[moveCount] = moveCount - 1;
        }
        else
        {
            irreversible[moveCount] = moveCount - 1 > 0 ? irreversible[moveCount - 1] : 0;
        }

        moveCount++;
    }

    private static bool IsIrreversible(ref readonly Move m)
    {
        return m.FromPieceType == PieceType.Pawn || m.CapturePiece != Piece.None;
    }

    public void Unwind() => moveCount--;

    public bool IsDraw(ulong key)
    {
        // if (moveCount - irreversible[moveCount - 1] >= 100) return true;

        for (int i = moveCount - 2; i > 0 && i >= irreversible[moveCount - 1]; i--)
        {
            if (history[i] == key) return true;
        }
        return false;
    }

    public void Clear() => moveCount = 0;
}