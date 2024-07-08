using Lolbot.Core;

namespace Lolbot.Tests;

public class TrickyPosition
{
    private readonly Position position;
    private readonly Move[] moves;

    public TrickyPosition()
    {
        position = Position.FromFen("rnbq1k1r/pp1Pbppp/2p5/8/2B5/8/PPP1NnPP/RNBQK2R w KQ - 1 8");
        moves = position.GenerateLegalMoves().ToArray();
    }

    [Test]
    public void Should_Have_Captures_With_Promotions()
    {
        moves.Should().IntersectWith([
            new Move("d7", "c8", "c8", 'b') with { PromotionPiece = Piece.WhiteKnight },
            new Move("d7", "c8", "c8", 'b') with { PromotionPiece = Piece.WhiteBishop },
            new Move("d7", "c8", "c8", 'b') with { PromotionPiece = Piece.WhiteRook },
            new Move("d7", "c8", "c8", 'b') with { PromotionPiece = Piece.WhiteQueen }
        ]);
    }

}