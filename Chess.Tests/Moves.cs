using Lolbot.Core;

namespace Lolbot.Tests;

public class Moves
{
    [Test]
    public void LegalPawnMovesAtStart()
    {
        var startMoves = new Position().GenerateLegalMoves(Color.White, Piece.WhitePawn);
        startMoves.Should().BeEquivalentTo([
            new Move("a2", "a3"), new Move("a2", "a4"),
            new Move("b2", "b3"), new Move("b2", "b4"),
            new Move("c2", "c3"), new Move("c2", "c4"),
            new Move("d2", "d3"), new Move("d2", "d4"),
            new Move("e2", "e3"), new Move("e2", "e4"),
            new Move("f2", "f3"), new Move("f2", "f4"),
            new Move("g2", "g3"), new Move("g2", "g4"),
            new Move("h2", "h3"), new Move("h2", "h4"),
        ]);
    }

    [Test]
    public void LegalBlackPawnMovesAtStart()
    {
        var startMoves = new Position().GenerateLegalMoves(Color.Black, Piece.BlackPawn);
        startMoves.Should().BeEquivalentTo([
            new Move("a7", "a6"), new Move("a7", "a5"),
            new Move("b7", "b6"), new Move("b7", "b5"),
            new Move("c7", "c6"), new Move("c7", "c5"),
            new Move("d7", "d6"), new Move("d7", "d5"),
            new Move("e7", "e6"), new Move("e7", "e5"),
            new Move("f7", "f6"), new Move("f7", "f5"),
            new Move("g7", "g6"), new Move("g7", "g5"),
            new Move("h7", "h6"), new Move("h7", "h5"),
        ]);
    }

    [Test]
    public void LegalKnightMovesAtStart()
    {
        var startMoves = new Position().GenerateLegalMoves(Color.White, Piece.WhiteKnight);
        startMoves.Should().BeEquivalentTo([
            new Move("b1", "a3"),
            new Move("b1", "c3"),

            new Move("g1", "h3"),
            new Move("g1", "f3"),
        ]);
    }


    [Test]
    public void Castle()
    {
        var game = Engine.NewGame();

        game = Engine.Move(game, "E2", "E4");
        game = Engine.Move(game, "E7", "E5");
        game = Engine.Move(game, "g1", "f3");
        game = Engine.Move(game, "d7", "d6");
        game = Engine.Move(game, "f1", "c4");
        game = Engine.Move(game, "f7", "f5");
        game = Engine.Move(game, Move.Castle(game.CurrentPlayer));

        var eval = Engine.Evaluate(game.CurrentPosition);
        eval.Should().Be(0);
    }

    [TestCase("A2", "A4", 8, 24)]
    [TestCase("E5", "D4", 36, 27)]
    [TestCase("H8", "A1", 63, 0)]
    [TestCase("B1", "H4", 1, 31)]
    public void CheckMoveType(string from, string to, byte fromIdx, byte toIdx)
    {
        var move = new Move(
            Squares.FromCoordinates(from),
            Squares.FromCoordinates(to)
        );

        move.FromIndex.Should().Be(fromIdx);
        move.ToIndex.Should().Be(toIdx);
    }

    [TestCase("A1", (string[])["b3", "c2"])]
    [TestCase("B1", (string[])["a3", "c3", "d2"])]
    public void KnightMoves(string square, string[] expectedSquares)
    {
        VerifyMovePattern(MovePatterns.KnightMoves, square, expectedSquares);
    }

    [TestCase("A2", (string[])["a3", "a4"])]
    public void PawnPushes(string square, string[] expectedSquares)
    {
        VerifyMovePattern(MovePatterns.WhitePawnPushes, square, expectedSquares);
    }

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
    public void RookMoves(string square, string[] expectedSquares)
    {
        VerifyMovePattern(MovePatterns.RookMoves, square, expectedSquares);
    }

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
        VerifyMovePattern(MovePatterns.BishopMoves, square, expectedSquares);
    }

    private void VerifyMovePattern(ulong[] pattern, string square, string[] expectedSquares)
    {
        var from = Squares.IndexFromCoordinate(square);
        var moves = pattern[from];

        Bitboards.ToCoordinates(moves)
            .Should().BeEquivalentTo(expectedSquares);
    }
}