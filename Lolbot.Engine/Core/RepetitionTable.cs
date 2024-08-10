namespace Lolbot.Core;

public class RepetitionTable
{
    private int moveCount = 0;

    private bool[] irreversible = new bool[256];
    private ulong[] positions = new ulong[256];

    public void Update(Move m, ulong key)
    {
        positions[moveCount++] = key;
    }

    public void Unwind() => moveCount--;

    public bool CheckPosition(ulong key)
    {
        ulong posCount = 0;

        for (int i = moveCount; i >= 0; i--)
        {
            if (positions[i] == key) posCount++;
            if (posCount >= 2) return true;
        }

        return false;
    }
}