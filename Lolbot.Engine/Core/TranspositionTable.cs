
using System.Diagnostics;
using static System.Math;

namespace Lolbot.Core;

public class TranspositionTable
{
    public const uint Size = 0x100_0000;
    public const uint Mask = 0xff_ffff;
    public const byte UpperBound = 1;
    public const byte LowerBound = 2;
    public const byte Exact = 3;

#if DEBUG
    public int set_count = 0;
    public int collision_count = 0;
    public int rewrite_count = 0;

    public double FillFactor => set_count / (128.0 * ushort.MaxValue);
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
            Depth = (byte)depth;
            Evaluation = (short)eval;
            Type = type;
            Move = move;
        }
    }

    private readonly Entry[] entries = new Entry[Size];


    public Entry Add(ulong hash, int depth, int eval, byte type, Move move)
    {
        var index = hash & Mask;

        Debug.Assert(eval < short.MaxValue);

#if DEBUG
        var current = entries[index];

        if (!current.IsSet) set_count++;
        else if (hash == current.Key) rewrite_count++;
        else if (hash != current.Key) collision_count++;
#endif

        return entries[index] = new Entry(hash, depth, eval, type, move);
    }

    public Entry Get(ulong hash)
    {
        var index = hash & Mask;

        return entries[index];
    }

    public bool Probe(ulong hash,
        int depth,
        ref int alpha,
        ref int beta,
        out Move move,
        out int eval)
    {
        var index = hash & Mask;
        ref var entry = ref entries[index];
        eval = entry.Evaluation;

        if (hash == entry.Key)
        {
            move = entry.Move;
            if (entry.Depth >= depth)
            {
                if (entry.Type == Exact) return true;
                else if (entry.Type == LowerBound) alpha = Max(alpha, entry.Evaluation);
                else if (entry.Type == UpperBound) beta = Min(beta, entry.Evaluation);

                if (alpha >= beta) return true;
            }
            return false;
        }

        move = Move.Null;
        return false;
    }

    public bool TryGet(ulong hash, out Entry entry)
    {
        entry = Get(hash);
        return hash == entry.Key;
    }


    public bool TryGet(ulong hash, int depth, out Entry entry)
    {
        entry = Get(hash);
        bool isMatch = entry.Depth >= depth && hash == entry.Key;
        // if (isMatch) Console.WriteLine($"info tt match: {entry.Key:X} [{entry.Type}]");

        return isMatch;
    }

    internal void Clear()
    {
        for (int i = 0; i < entries.Length; i++)
        {
            entries[i] = new Entry();
        }
#if DEBUG
        set_count = 0;
        collision_count = 0;
        rewrite_count = 0;
#endif
    }
}