namespace Lolbot.Core;

public class TranspositionTable
{
    public readonly struct Entry
    {
        public readonly ushort Key;
        public readonly int Depth, Alpha, Beta;
        public readonly Move BestMove;
        public readonly bool IsSet;

        public Entry(ushort key, int depth, int alpha, int beta, Move bestMove)
        {
            Key = key;
            Depth = depth;
            Alpha = alpha;
            Beta = beta;
            BestMove = bestMove;
            IsSet = true;
        }
    }

    private readonly Entry[] entries = new Entry[ushort.MaxValue];

    public Entry Add(ulong key, int depth, int alpha, int beta, Move bestMove)
    {
        var index = key & 0xffff;
        index ^= key >> 16 & 0xffff;
        index ^= key >> 32 & 0xffff;
        index ^= key >> 48 & 0xffff;

        return entries[index] = new Entry((ushort)index, depth, alpha, beta, bestMove);
    }

    public Entry Get(ulong key)
    {
        var index = key & 0xffff;
        index ^= key >> 16 & 0xffff;
        index ^= key >> 32 & 0xffff;
        index ^= key >> 48 & 0xffff;

        return entries[index];
    }

}