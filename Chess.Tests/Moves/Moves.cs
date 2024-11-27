using System.Diagnostics;
using Lolbot.Core;

namespace Lolbot.Tests;

[Category(TestSuites.Fast)]
public class Moves
{
    [Test]
    public void LegalPawnMovesAtStart()
    {
        var startMoves = new MutablePosition().GenerateLegalMoves(Piece.WhitePawn);
        startMoves.ToArray().Should().BeEquivalentTo([
            new Move('P', "a2", "a3"), new Move('P', "a2", "a4"),
            new Move('P', "b2", "b3"), new Move('P', "b2", "b4"),
            new Move('P', "c2", "c3"), new Move('P', "c2", "c4"),
            new Move('P', "d2", "d3"), new Move('P', "d2", "d4"),
            new Move('P', "e2", "e3"), new Move('P', "e2", "e4"),
            new Move('P', "f2", "f3"), new Move('P', "f2", "f4"),
            new Move('P', "g2", "g3"), new Move('P', "g2", "g4"),
            new Move('P', "h2", "h3"), new Move('P', "h2", "h4"),
        ]);
    }

    [Test]
    public void Each_Player_Should_Have_20_Legal_Moves_From_Start_Position()
    {
        var position = new MutablePosition();
        var sw = Stopwatch.StartNew();
        var whiteMoves = position
            .GenerateLegalMoves();

        position.SkipTurn();
        var blackMoves = position
            .GenerateLegalMoves();

        sw.Stop();
        Console.WriteLine(sw.ElapsedTicks);
        whiteMoves.ToArray().Should().HaveCount(20);
        blackMoves.ToArray().Should().HaveCount(20);
    }

    [Test]
    public void LegalBlackPawnMovesAtStart()
    {
        var position = new MutablePosition();
        position.SkipTurn();
        var startMoves = position.GenerateLegalMoves(Piece.BlackPawn);

        startMoves.ToArray().Should().BeEquivalentTo([
            new Move('p', "a7", "a6"), new Move('p', "a7", "a5"),
            new Move('p', "b7", "b6"), new Move('p', "b7", "b5"),
            new Move('p', "c7", "c6"), new Move('p', "c7", "c5"),
            new Move('p', "d7", "d6"), new Move('p', "d7", "d5"),
            new Move('p', "e7", "e6"), new Move('p', "e7", "e5"),
            new Move('p', "f7", "f6"), new Move('p', "f7", "f5"),
            new Move('p', "g7", "g6"), new Move('p', "g7", "g5"),
            new Move('p', "h7", "h6"), new Move('p', "h7", "h5"),
        ]);
    }

    [Test]
    public void LegalKnightMovesAtStart()
    {
        var startMoves = new MutablePosition().GenerateLegalMoves(Piece.WhiteKnight);
        startMoves.ToArray().Should().BeEquivalentTo([
            new Move('N', "b1", "a3"),
            new Move('N', "b1", "c3"),

            new Move('N', "g1", "h3"),
            new Move('N', "g1", "f3"),
        ]);
    }


    // [Test]
    // public void Castle()
    // {
    //     var game = Engine.NewGame();

    //     game = Engine.Move(game, "E2", "E4");
    //     game = Engine.Move(game, "E7", "E5");
    //     game = Engine.Move(game, "g1", "f3");
    //     game = Engine.Move(game, "d7", "d6");
    //     game = Engine.Move(game, "f1", "c4");
    //     game = Engine.Move(game, "f7", "f5");
    //     game = Engine.Move(game, Move.Castle(game.CurrentPlayer));

    //     var eval = Engine.Evaluate(game.CurrentPosition);
    //     eval.Should().Be(0);
    // }

    [TestCase("A2", "A4", 8, 24)]
    [TestCase("E5", "D4", 36, 27)]
    [TestCase("H8", "A1", 63, 0)]
    [TestCase("B1", "H4", 1, 31)]
    public void From_Coordinates_Should_Map_To_Indices(string from, string to, byte fromIdx, byte toIdx)
    {
        var move = new Move('Q', from, to);

        move.FromIndex.Should().Be(fromIdx);
        move.ToIndex.Should().Be(toIdx);
    }

    [TestCase("A1", (string[])["b3", "c2"])]
    [TestCase("B1", (string[])["a3", "c3", "d2"])]
    public void KnightMoves(string square, string[] expectedSquares)
    {
        VerifyMovePattern(MovePatterns.Knights, square, expectedSquares);
    }

    [TestCase("A2", (string[])["a3", "a4"])]
    public void PawnPushes(string square, string[] expectedSquares)
    {
        VerifyMovePattern(MovePatterns.WhitePawnPushes, square, expectedSquares);
    }


    [TestCase("a1", "h8", new[] { "b2", "c3", "d4", "e5", "f6", "g7", "h8" })]
    [TestCase("a8", "h1", new[] { "b7", "c6", "d5", "e4", "f3", "g2", "h1" })]

    [TestCase("a1", "a4", new[] { "a2", "a3", "a4" })]
    [TestCase("h7", "h7", new[] { "h7" })]

    [TestCase("a1", "b8", new[] { "b8" })]
    [TestCase("b5", "e8", new[] { "c6", "d7", "e8" })]
    public void SquaresBetween(string f, string t, string[] squares)
    {
        byte from = Squares.IndexFromCoordinate(f);
        byte to = Squares.IndexFromCoordinate(t);
        var squaresBetween = MovePatterns.SquaresBetween[from][to];

        squaresBetween.Should().Be(Bitboards.Create(squares));
    }

    [Test]
    public void Capture_Should_Update_Boards()
    {
        var pos = MutablePosition.FromFen("4r1k1/1b3rp1/1n3q1p/2p1N3/1p6/7P/PP3PP1/R2QR1K1 w - - 0 25");
        var game = new Game(pos, []);
        Engine.Move(game, "e5", "f7");
        Engine.Move(game, "e8", "e1");
        
        game.CurrentPosition.WhiteRooks.Should().Be(1ul << Squares.A1);
        game.CurrentPosition.BlackRooks.Should().Be(1ul << Squares.E1);

        (game.CurrentPosition.White & (1ul << Squares.E1)).Should().Be(0);
        (game.CurrentPosition.Black & (1ul << Squares.E1)).Should().Be(1ul << Squares.E1);
    }

    internal static void VerifyMovePattern(ulong[] pattern, string square, string[] expectedSquares)
    {
        var from = Squares.IndexFromCoordinate(square);
        var moves = pattern[from];

        Bitboards.ToCoordinates(moves)
            .Should().BeEquivalentTo(expectedSquares);
    }
}