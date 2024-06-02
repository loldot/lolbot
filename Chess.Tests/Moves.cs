using Chess.Api;

namespace Chess.Tests;

public class Moves
{
    [Test]
    public void GeneratePawnAttaks()
    {
        var pawnstructure = Utils.FromArray([
            0,0,0,0,0,0,0,0,
            0,0,0,0,0,0,0,0,
            0,0,0,0,0,0,0,0,
            0,0,0,0,0,0,0,0,
            0,0,1,0,0,0,0,0,
            1,1,0,1,0,1,1,1,
            0,0,0,0,0,0,0,0
        ]);

        var pos = new Position() with
        {
            WhitePawns = pawnstructure
        };

        pos.WhitePawnAttacks().Should().Be(Utils.FromArray([
            0,0,0,0,0,0,0,0,
            0,0,0,0,0,0,0,0,
            0,0,0,0,0,0,0,0,
            0,1,0,1,0,0,0,0,
            1,1,1,0,1,1,1,1,
            0,0,0,0,0,0,0,0,
            0,0,0,0,0,0,0,0
        ]));
    }

    [Test]
    public void LegalPawnFirstPawnMoves()
    {
        var pos = new Position();
        pos.LegalMoves(Piece.WhitePawn).Should().Be(Utils.FromArray([
            0,0,0,0,0,0,0,0,
            0,0,0,0,0,0,0,0,
            0,0,0,0,0,0,0,0,
            0,0,0,0,0,0,0,0,
            1,1,1,1,1,1,1,1,
            1,1,1,1,1,1,1,1,
            0,0,0,0,0,0,0,0,
            0,0,0,0,0,0,0,0
        ]));
    }

    [Test]
    public void LegalPawnMovesFrom3rdRank()
    {
        var pos = new Position() with
        {
            WhitePawns = 0xff0000
        };
        var legalMoves = pos.LegalMoves(Piece.WhitePawn);
        legalMoves.Should().Be(Utils.FromArray([
            0,0,0,0,0,0,0,0,
            0,0,0,0,0,0,0,0,
            0,0,0,0,0,0,0,0,
            0,0,0,0,0,0,0,0,
            1,1,1,1,1,1,1,1,
            0,0,0,0,0,0,0,0,
            0,0,0,0,0,0,0,0,
            0,0,0,0,0,0,0,0
        ]));
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
        Console.WriteLine(game.CurrentPosition);

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
}