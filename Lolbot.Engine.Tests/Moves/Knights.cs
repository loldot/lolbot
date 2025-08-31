using Lolbot.Core;

namespace Lolbot.Tests;

public class Knights
{
    [Test]
    public void Should_Have_Captures()
    {
        var fen = "8/3k4/4n1n1/3n3n/5N2/3n3n/4n1n1/1K6 w - - 0 1";
        var pos = MutablePosition.FromFen(fen);

        Span<Move> moves = new Move[255];
        var count = MoveGenerator.Captures(pos, ref moves);

        count.Should().Be(8);
    }
}