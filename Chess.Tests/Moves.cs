using Chess.Api;

namespace Chess.Tests;

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
    public void LegaalKnightMovesAtStart()
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
            Utils.SquareFromCoordinates(from),
            Utils.SquareFromCoordinates(to)
        );

        move.FromIndex.Should().Be(fromIdx);
        move.ToIndex.Should().Be(toIdx);
    }

    [TestCase("A1", (string[])["b3", "c2"])]
    [TestCase("B1", (string[])["a3", "c3", "d2"])]
    public void KnightMoves(string square, string[] expectedSquares)
    {
        var from = Utils.IndexFromCoordinate(square);
        var moves = MovePatterns.KnightMoves[from];

        Utils.BitboardToCoords(moves)
            .Should().BeEquivalentTo(expectedSquares);
    }

    [TestCase("A2", (string[])["a3", "a4", "b3"])]
    public void PawnMoves(string square, string[] expectedSquares)
    {
        var from = Utils.IndexFromCoordinate(square);
        var moves = MovePatterns.PawnMoves[from];

        Utils.BitboardToCoords(moves)
            .Should().BeEquivalentTo(expectedSquares);
    }
}