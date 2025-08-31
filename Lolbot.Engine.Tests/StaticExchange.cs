using Lolbot.Core;
namespace Lolbot.Tests;

public class StaticExchangeTests
{
    [Test]
    public void SEE_Should_Be_Positive_When_No_Further_Captures()
    {
        var position = MutablePosition.FromFen("1k6/8/8/3p4/3Q4/8/8/5K2 w - - 0 1");
        var seeVal = position.SEE(new Move('Q', "D4", "D5", 'p'));
        seeVal.Should().Be(Heuristics.PawnValue);
    }

    [Test]
    public void SEE_Should_Evaluate_All_Attackers()
    {
        var position = MutablePosition.FromFen("1k6/4n3/8/3p4/3Q4/8/8/5K2 w - - 0 1");
        var seeVal = position.SEE(new Move('Q', "D4", "D5", 'p'));
        seeVal.Should().Be(-Heuristics.QueenValue + Heuristics.PawnValue);
    }

    [Test]
    public void SEE_Should_Consider_Not_Capturing()
    {
        var position = MutablePosition.FromFen("1k6/8/q7/3p3r/2R5/2N1N3/4B3/3Q1K2 b - - 0 1");
        var seeVal = position.SEE(new Move('p', "D5", "C4", 'R'));
        seeVal.Should().Be(Heuristics.RookValue - Heuristics.PawnValue);
    }

    [Test]
    public void SEE_Should_Handle_XRays()
    {
        var position = MutablePosition.FromFen("1k1r3q/1ppn3p/p4b2/4p3/8/P2N2P1/1PP1R1BP/2K1Q3 w - -");
        var seeVal = position.SEE(new Move('N', "D3", "E5", 'p'));
        seeVal.Should().Be(-Heuristics.KnightValue + Heuristics.PawnValue);
    }

    [Test]
    public void SEE_Should_Handle_EnPassant()
    {
        var pos = MutablePosition.FromFen("3k4/8/8/3p1p2/4p3/3P1P2/8/2K5 w - - 0 1");
        var seeVal = pos.SEE(new Move('P', "D3", "E4", 'p'));
        seeVal.Should().Be(0);
    }
}