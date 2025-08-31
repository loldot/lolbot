using Lolbot.Core;

namespace Lolbot.Tests;

public class StaticExchangeTests
{
    [Test]
    public void TestStaticExchange()
    {
        var position = MutablePosition.FromFen("1k6/8/8/3p4/3Q4/8/8/5K2 w - - 0 1");
        var see = position.SEE(new Move('Q', "D4", "D5", 'p'));
        see.Should().Be(Heuristics.PawnValue);
    }
}