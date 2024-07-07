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

    [Test]
    public void Should_Not_Jump()
    {
        var position = Position.FromFen("r2k1nr1/5p2/2ppp3/8/8/3q4/PP1PNPPP/R1RQK3 w - - 0 1");
        var moves = position.GenerateLegalMoves('P').ToArray();
        moves.Should().NotContain(x => x == new Move("d2","d4"));
    }

    [Test]
    public void Should_Promote()
    {
        var position = Position.FromFen("3n3k/P7/8/8/8/8/8/K7 w - - 0 1");
        var moves = position.GenerateLegalMoves().ToArray();
        
        moves.Should().Contain(x => x.PromotionPiece == Piece.WhiteBishop);
        moves.Should().Contain(x => x.PromotionPiece == Piece.WhiteKnight);
        moves.Should().Contain(x => x.PromotionPiece == Piece.WhiteRook);
        moves.Should().Contain(x => x.PromotionPiece == Piece.WhiteQueen);
    }
}