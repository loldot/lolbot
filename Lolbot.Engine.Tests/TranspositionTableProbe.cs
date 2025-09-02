using Lolbot.Core;

namespace Lolbot.Tests;

[Category(TestSuites.Fast)]
public class TranspositionTableProbeTests
{
    [Test]
    public void Probe_Should_Return_Exact_Hit()
    {
        var tt = new TranspositionTable();
        ulong hash = 0xdead_beef_cafe_babeul;
        var move = new Move('P', "e2", "e4");

        tt.Add(hash, depth: 6, eval: 123, TranspositionTable.Exact, move);

        int alpha = -9999, beta = 9999, eval;
        var hit = tt.Probe(hash, depth: 4, ref alpha, ref beta, out var probedMove, out eval);

        hit.Should().BeTrue();
        eval.Should().Be(123);
        probedMove.Should().Be(move);
    }

    [Test]
    public void Probe_LowerBound_Should_Raise_Alpha_Without_Cutoff()
    {
        var tt = new TranspositionTable();
        ulong hash = 0xabad1dea_1337_0001ul;

        tt.Add(hash, depth: 8, eval: 50, TranspositionTable.LowerBound, new Move('N', "g1", "f3"));

        int alpha = 0, beta = 100, eval;
        var hit = tt.Probe(hash, depth: 4, ref alpha, ref beta, out var _, out eval);

        hit.Should().BeFalse();
        alpha.Should().Be(50);
        beta.Should().Be(100);
        eval.Should().Be(50);
    }

    [Test]
    public void Probe_UpperBound_Should_Lower_Beta_And_Cutoff_When_Alpha_Ge_Beta()
    {
        var tt = new TranspositionTable();
        ulong hash = 0xabad1dea_1337_0002ul;

        tt.Add(hash, depth: 8, eval: 50, TranspositionTable.UpperBound, new Move('B', "c4", "b5"));

        int alpha = 60, beta = 70, eval;
        var hit = tt.Probe(hash, depth: 4, ref alpha, ref beta, out var move, out eval);

        hit.Should().BeTrue();
        beta.Should().Be(50); // lowered to upper bound
        alpha.Should().Be(60);
        eval.Should().Be(50);
        move.Should().NotBe(Move.Null);
    }
}
