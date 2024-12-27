using Lolbot.Core;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;


var summary = BenchmarkRunner.Run<Perft5Bench>();

[DisassemblyDiagnoser]
public class Perft5Bench()
{
    MutablePosition pos = MutablePosition.FromFen("Q2R2Q1/8/2ppp3/R1pkp2R/2ppp3/8/B7/1K1R3Q b - - 0 1");

    [Benchmark]
    public void Current()
    {
        var x = pos.CreatePinmasksOld(Colors.Black);
    }

    [Benchmark]
    public void New()
    {
        var x = pos.CreatePinmasks(Colors.Black);
    } 
}
