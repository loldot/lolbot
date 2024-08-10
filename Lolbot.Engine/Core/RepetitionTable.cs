
namespace Lolbot.Core;

public class RepetitionTable
{
    private int moveCount = 0;

    private int[] irreversible = new int[256];
    private ulong[] history = new ulong[256];

    public void Update(Move m, ulong key)
    {
        history[moveCount++] = key;
        irreversible[moveCount] = IsIrreversible(ref m) ? moveCount - 1 : irreversible[moveCount - 1];
    }

    private static bool IsIrreversible(ref readonly Move m)
    {
        return ((byte)m.FromPiece & 0xf) == (byte)PieceType.Pawn || m.CapturePiece != Piece.None;
    }

    public void Unwind() => moveCount--;

    public bool IsDrawByRepetition(ulong key)
    {
        ulong posCount = 0;

        // Console.WriteLine(key);
        // Console.WriteLine($"MoveCount: {moveCount}");
        // Console.WriteLine($"Irrev: [{string.Join(',', irreversible[..moveCount])}]");

        // Console.WriteLine($"History:");

        for (int i = moveCount - 1; i >= irreversible[moveCount - 1]; i--)
        {
            // Console.Write(history[i]);
            if (history[i] == key)
            {
                posCount++;
                // Console.Write("*");               
            }
            if (posCount >= 2) return true;

            // Console.WriteLine();
        }

        return false;
    }
}