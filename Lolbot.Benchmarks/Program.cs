using System;
using System.Security.Cryptography;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;

var summary = BenchmarkRunner.Run<LmrBenchmark>();

public class LmrBenchmark
{
    private static readonly float[] LogTableF = new float[256];
    private static readonly uint[] LogTable = new uint[256];

    static LmrBenchmark()
    {
        for (int i = 1; i < 256; i++)
        {
            LogTableF[i] = MathF.Log(i);
            LogTable[i] = (uint)(1024 * MathF.Log(i));
        }
    }

    [Benchmark]
    public int Lmr_Int_Log_Table()
    {
        uint sum = 0;
        for (byte depth = 0; depth < 25; depth++)
        {
            for (byte move = 0; move < 56; move++)
            {
                sum += 1 + ((LogTable[depth] * LogTable[move + 1]) >> 11);
            }
        }
        return (int)sum;
    }

    [Benchmark]
    public int Lmr_Float_Log_Table()
    {
        int sum = 0;
        for (byte depth = 0; depth < 25; depth++)
        {
            for (byte move = 0; move < 56; move++)
            {
                sum += 1 + (int)(LogTableF[depth] * LogTableF[move + 1]  / 2);
            }
        }
        return sum;
    }

    [Benchmark]
    public int Lmr_Float()
    {
        int sum = 0;
        for (byte depth = 0; depth < 25; depth++)
        {
            for (byte move = 0; move < 56; move++)
            {
                sum += 1 + (int)(MathF.Log(depth) * MathF.Log(move + 1) / 2);
            }
        }
        return sum;
    }
}