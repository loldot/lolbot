using Lolbot.Core;

namespace Lolbot.Tests;

public class Rooks
{
    [TestCase("A2", (string[])[
        "a1", "a3", "a4", "a5", "a6", "a7", "a8",
        "b2", "c2", "d2", "e2", "f2", "g2", "h2"
    ])]
    [TestCase("E4", (string[])[
        "e1", "e2", "e3", "e5", "e6", "e7", "e8",
        "a4", "b4", "c4", "d4", "f4", "g4", "h4"
    ])]
    [TestCase("H8", (string[])[
        "h1", "h2", "h3", "h4", "h5", "h6", "h7",
        "a8", "b8", "c8", "d8", "e8", "f8", "g8"
    ])]
    public void Move_On_Empty_Board_Should_Be(string square, string[] expectedSquares)
    {
        Moves.VerifyMovePattern(MovePatterns.Rooks, square, expectedSquares);
    }

    [Test]
    public void RookBlockersUp()
    {
        var rooks = Bitboards.Create("A1", "H1");
        var blockers = Bitboards.Create("A3", "A4", "C1", "E1", "H6", "H7", "E5");

        var rookMoves = MovePatterns.RookAttacks(rooks, ~blockers);
        rookMoves.Should().Be(Bitboards.Create((int[])[
            0,0,0,0,0,0,0,0,
            0,0,0,0,0,0,0,0,
            0,0,0,0,0,0,0,1,
            0,0,0,0,0,0,0,1,
            0,0,0,0,0,0,0,1,
            1,0,0,0,0,0,0,1,
            1,0,0,0,0,0,0,1,
            0,1,1,0,1,1,1,0
        ]));
    }

    [Test]
    public void BlackCastle()
    {
        var g = new Game(new Position(), [
            new Move("e2", "e4"),
            new Move("e7", "e5"),
            new Move("d2", "d4"),
            new Move("f8", "d6"),
            new Move("d4", "d5"),
            new Move("g8", "f6"),
            new Move("f2", "f3"),
            Move.Castle(Color.Black)
        ]);
        var rooks = g.CurrentPosition.BlackRooks;
        
        rooks.Should().Be(Bitboards.Create("a8", "f8"));

    }
}