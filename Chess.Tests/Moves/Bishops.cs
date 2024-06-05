using Lolbot.Core;

namespace Lolbot.Tests;

public class Bishops
{
    [TestCase("A2", (string[])[
        "b1", "b3", "c4", "d5", "e6", "f7", "g8"
    ])]
    [TestCase("E4", (string[])[
        "b1", "c2", "d3", "f5", "g6", "h7",
        "a8", "b7", "c6", "d5", "f3", "g2", "h1"
    ])]
    [TestCase("H8", (string[])[
        "a1", "b2", "c3", "d4", "e5", "f6", "g7"
    ])]
    public void BishopMoves(string square, string[] expectedSquares)
    {
        Moves.VerifyMovePattern(MovePatterns.Bishops, square, expectedSquares);
    }

    [Test]
    public void BishopsFromCorners()
    {
        var bishops = Bitboards.Create("A1", "H1");
        var blockers = Bitboards.Create("D5", "F6");

        var bishopMoves = MovePatterns.BishopAttacks(bishops, ~blockers);
        bishopMoves.Should().Be(Bitboards.Create((int[])[
            0,0,0,0,0,0,0,0,
            0,0,0,0,0,0,0,0,
            0,0,0,0,0,1,0,0,
            0,0,0,1,1,0,0,0,
            0,0,0,1,1,0,0,0,
            0,0,1,0,0,1,0,0,
            0,1,0,0,0,0,1,0,
            0,0,0,0,0,0,0,0
        ]));
    }
}