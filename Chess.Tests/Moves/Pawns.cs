using Lolbot.Core;

public class Pawns
{
    [Test]
    public void Should_Be_Blocked_By_Opponent()
    {
        var position = new Position() with {
            BlackPawns = 0x404002000000,
            WhitePawns = 0x40220000
        };

        var blackMoves = position.GenerateLegalMoves(Color.Black, Piece.BlackPawn);
        var whiteMoves = position.GenerateLegalMoves(Color.White, Piece.WhitePawn);

        whiteMoves.Should().BeEquivalentTo([new Move("f3", "f4")]);
        blackMoves.Should().BeEmpty();
    }
}