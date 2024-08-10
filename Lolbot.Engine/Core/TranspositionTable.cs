
using System.Diagnostics;

namespace Lolbot.Core;

public class TranspositionTable
{
    public static readonly byte Alpha = 1;
    public static readonly byte Beta = 2;
    public static readonly byte Exact = 3;

    public static readonly ulong BucketMask = 0xff;

    public int set_count = 0;
    public int collision_count = 0;
    public int rewrite_count = 0;

    public double FillFactor => set_count / (256.0 * (ushort.MaxValue + 1));


    public readonly struct Entry
    {
        public readonly byte Type;
        public readonly bool IsSet;
        public readonly int Depth, Evaluation;
        public readonly ulong Key;
        
        public Entry(ulong key, int depth, int eval, byte type)
        {
            Key = key;
            Depth = depth;
            Evaluation = eval;
            Type = type;
            IsSet = true;
        }

        override public string ToString()
        {
            return $"d:{Depth}, alpha: {Evaluation}, beta: {Beta}";
        }
    }

    private readonly Entry[][] entries = new Entry[256][];
    public TranspositionTable()
    {
        for (int i = 0; i < entries.Length; i++)
        {
            entries[i] = new Entry[ushort.MaxValue + 1];
        }
    }

    public Entry Add(ulong hash, int depth, int eval, byte type)
    {
        var index = hash.GetHashCode() & 0xffff;

        Debug.Assert(index <= ushort.MaxValue);

        var current = entries[(byte)(hash & BucketMask)][(ushort)index];
        if (!current.IsSet) set_count++;
        else if (hash == current.Key) rewrite_count++;
        else if (hash != current.Key) collision_count++;

        return entries[(byte)(hash & BucketMask)][(ushort)index] = new Entry(hash, depth, eval, type);
    }

    public Entry Get(ulong hash)
    {
        var index = hash.GetHashCode() & 0xffff;

        Debug.Assert(index <= ushort.MaxValue);

        return entries[(byte)(hash & BucketMask)][(ushort)index];
    }

    public bool TryGet(ulong hash, int depth, out Entry entry)
    {
        entry = Get(hash);
        return entry.IsSet && entry.Depth >= depth && hash == entry.Key;
    }
}