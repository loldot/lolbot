using Lolbot.Core;

namespace Lolbot.Tests;

[Category(TestSuites.Fast)]
public class TrickyPosition
{
    private readonly MutablePosition position;
    private readonly Move[] moves;

    public TrickyPosition()
    {
        position = MutablePosition.FromFen("rnbq1k1r/pp1Pbppp/2p5/8/2B5/8/PPP1NnPP/RNBQK2R w KQ - 1 8");
        moves = position.GenerateLegalMoves().ToArray();
    }

    [Test]
    public void Should_Have_Captures_With_Promotions()
    {
        moves.Should().IntersectWith([
            Move.PromoteWithCapture('P', "d7", "c8", 'b', 'N'),
            Move.PromoteWithCapture('P', "d7", "c8", 'b', 'B'),
            Move.PromoteWithCapture('P', "d7", "c8", 'b', 'R'),
            Move.PromoteWithCapture('P', "d7", "c8", 'b', 'Q')
        ]);
    }
    [Test]
    public void Should_Have_KingsideCastle()
    {
        moves.Should().IntersectWith([
            Move.Castle(Colors.White)
        ]);
    }
}