using Lolbot.Core;

namespace Lolbot.Tests;
public class Pawns
{
    [Test]
    public void Should_Be_Blocked_By_Opponent()
    {
        var position = new Position() with
        {
            BlackPawns = 0x404002000000,
            WhitePawns = 0x40220000
        };

        var blackMoves = position.GenerateLegalMoves(Color.Black, Piece.BlackPawn);
        var whiteMoves = position.GenerateLegalMoves(Color.White, Piece.WhitePawn);

        whiteMoves.ToArray().Should().BeEquivalentTo([new Move("f3", "f4")]);
        blackMoves.ToArray().Should().BeEmpty();
    }

    [Test]
    public void Should_Capture_En_Passant_Square()
    {
        var game = Engine.NewGame();
        game = Engine.Move(game, "e2", "e4");
        game = Engine.Move(game, "d7", "d5");
        game = Engine.Move(game, "f1", "e2");
        game = Engine.Move(game, "d5", "e4");
        game = Engine.Move(game, "f2", "f4");
        game = Engine.Move(game, "e4", "f3");

        Bitboards.Debug(game.CurrentPosition.WhitePawns);
    }
}