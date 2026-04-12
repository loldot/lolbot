using Lolbot.Core;

namespace Lolbot.Tests;

[Category(TestSuites.Fast)]
public class EnPassant
{

    [Test]
    public void EnPassant_Should_Not_Be_Set_When_No_Enemy_Pawns_Can_Take()
    {
        var position = new MutablePosition();
        var m = new Move('P', "e2", "e4");
        position.Move(ref m);
        position.EnPassant.Should().Be(0);
    }

    [Test]
    public void EnPassant_Should_Not_Be_Set_When_No_Enemy_Pawns_Can_Take_As_Black()
    {
        var position = new MutablePosition();
        var m = new Move('p', "e7", "e5");
        position.Move(ref m);
        position.EnPassant.Should().Be(0);
    }

    [Test]
    public void EnPassant_Should_Not_Be_Set_On_Single_Push()
    {
        var position = new MutablePosition();
        var m = new Move('P', "e2", "e3");
        position.Move(ref m);
        position.EnPassant.Should().Be(0);
    }

    [Test]
    public void EnPassant_Should_Not_Be_Set_When_NextToWrapsBoard()
    {
        var position = MutablePosition.FromFen("3k3P/p6P/7P/7P/7P/7P/7P/3K3P b - - 0 1");
        var m = new Move('p', "a7", "a5");
        position.Move(ref m);
        position.EnPassant.Should().Be(0);
    }

    [Test]
    public void EnPassant_Should_Be_Set_When_Opponent_Can_Take()
    {
        var position = MutablePosition.FromFen("3k4/p7/7P/1P5P/8/8/8/3K4 b - - 0 1");
        var m = new Move('p', "a7", "a5");
        position.Move(ref m);
        position.EnPassant.Should().Be(Squares.A6);
    }

    
    [Test]
    public void EnPassant_Should_Not_Be_Set_When_Pawn_Is_Pinned()
    {
        var pos = MutablePosition.FromFen("5k2/1r3p2/3p1p1p/1Pp2B1P/1KR3P1/8/8/8 w - c6 0 78");
        var illegalEP = new Move(Piece.WhitePawn, 
            Squares.IndexFromCoordinate("b5"), 
            Squares.IndexFromCoordinate("c6"),
            Piece.BlackPawn,
            Squares.IndexFromCoordinate("c5"));
        
        pos.GenerateLegalMoves().ToArray().Should().NotContain(illegalEP);
        // pos.EnPassant.Should().NotBe(Squares.C6);
    }
}