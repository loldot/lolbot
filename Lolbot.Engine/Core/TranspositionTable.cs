
using System.Diagnostics;
using System.Reflection.Metadata.Ecma335;
using System.Xml.Serialization;

namespace Lolbot.Core;

public class TranspositionTable
{
    public static readonly byte Alpha = 1;
    public static readonly byte Beta = 2;
    public static readonly byte Exact = 3;

    public static readonly ulong BucketMask = 0xff;


    public readonly struct Entry
    {
        public readonly ushort Key;
        public readonly int Depth, Evaluation;
        public readonly byte Type;
        public readonly Move BestMove;
        public readonly bool IsSet;

        public Entry(ushort key, int depth, int eval, byte type, Move bestMove)
        {
            Key = key;
            Depth = depth;
            Evaluation = eval;
            Type = type;
            BestMove = bestMove;
            IsSet = true;
        }

        override public string ToString()
        {
            return $"[{BestMove}] d:{Depth}, alpha: {Evaluation}, beta: {Beta}";
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

    public Entry Add(ulong key, int depth, int eval, byte type, Move bestMove)
    {
        var index = key & 0xffff;
        index ^= key >> 16 & 0xffff;
        index ^= key >> 32 & 0xffff;
        index ^= key >> 48 & 0xffff;

        Debug.Assert(index <= ushort.MaxValue);

        return entries[(byte)(key & BucketMask)][(ushort)index] = new Entry((ushort)index, depth, eval, type, bestMove);
    }

    public Entry Get(ulong key)
    {
        var index = key & 0xffff;
        index ^= key >> 16 & 0xffff;
        index ^= key >> 32 & 0xffff;
        index ^= key >> 48 & 0xffff;

        Debug.Assert(index <= ushort.MaxValue);

        return entries[(byte)(key & BucketMask)][(ushort)index];
    }

    public bool TryGet(ulong hash, int depth, out Entry entry)
    {
        entry = Get(hash);
        return entry.IsSet && entry.Depth >= depth && hash == entry.Key;
    }
}