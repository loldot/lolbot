
using System.Diagnostics;

namespace Lolbot.Core;

public class TranspositionTable
{
    public const byte UpperBound = 1;
    public const byte LowerBound = 2;
    public const byte Exact = 3;

    public const ulong BucketMask = 0xff;

#if DEBUG
    public int set_count = 0;
    public int collision_count = 0;
    public int rewrite_count = 0;

    public double FillFactor => set_count / (256.0 * ushort.MaxValue);
#endif


    public readonly struct Entry
    {
        public readonly ulong Key;
        public readonly byte Type;
        public readonly byte Depth;
        public readonly short Evaluation;
        public readonly bool IsSet => Key != 0;

        public readonly Move Move;

        public Entry(ulong key, int depth, int eval, byte type, Move move)
        {
            Key = key;
            Depth = unchecked((byte)depth);
            Evaluation = unchecked((short)eval);
            Type = type;
            Move = move;
        }
    }

    private readonly Entry[][] entries = new Entry[256][];
    public TranspositionTable()
    {
        for (int i = 0; i < entries.Length; i++)
        {
            entries[i] = new Entry[ushort.MaxValue];
        }
    }

    public Entry Add(ulong hash, int depth, int eval, byte type, Move move)
    {
        var index = (hash & 0xfffe0000) >> 16;

        Debug.Assert(index <= ushort.MaxValue);

        var current = entries[(byte)(hash & BucketMask)][(ushort)index];
#if DEBUG
        if (!current.IsSet) set_count++;
        else if (hash == current.Key) rewrite_count++;
        else if (hash != current.Key) collision_count++;
#endif

        return entries[(byte)(hash & BucketMask)][(ushort)index] = new Entry(hash, depth, eval, type, move);
    }

    public Entry Get(ulong hash)
    {
        var index = (hash & 0xfffe0000) >> 16;

        Debug.Assert(index <= ushort.MaxValue);

        return entries[(byte)(hash & BucketMask)][(ushort)index];
    }

    public bool TryGet(ulong hash, out Entry entry)
    {
        entry = Get(hash);
        return entry.IsSet && hash == entry.Key;
    }


    public bool TryGet(ulong hash, int depth, out Entry entry)
    {
        entry = Get(hash);
        bool isMatch = entry.IsSet && entry.Depth >= depth && hash == entry.Key;
        // if (isMatch) Console.WriteLine($"info tt match: {entry.Key:X} [{entry.Type}]");

        return isMatch;
    }

    internal void Clear()
    {
        for (int i = 0; i < entries.Length; i++)
        {
            entries[i] = new Entry[ushort.MaxValue];
        }
#if DEBUG
        set_count = 0;
        collision_count = 0;
        rewrite_count = 0;
#endif
    }
}