using Lolbot.Core;

namespace Lolbot.Tests;

[Category(TestSuites.Fast)]
public class EnPassant
{

    [Test]
    public void EnPassant_Should_Not_Be_Set_When_No_Enemy_Pawns_Can_Take()
    {
        var position = new Position()
            .Move(new Move('P', "e2", "e4"));
        position.EnPassant.Should().Be(0);
    }

    [Test]
    public void EnPassant_Should_Not_Be_Set_When_No_Enemy_Pawns_Can_Take_As_Black()
    {
        var position = new Position()
            .Move(new Move('p', "e7", "e5"));
        position.EnPassant.Should().Be(0);
    }

    [Test]
    public void EnPassant_Should_Not_Be_Set_On_Single_Push()
    {
        var position = new Position()
            .Move(new Move('P', "e2", "e3"));
        position.EnPassant.Should().Be(0);
    }

    [Test]
    public void EnPassant_Should_Not_Be_Set_When_NextToWrapsBoard()
    {
        var position = Position.FromFen("3k3P/p6P/7P/7P/7P/7P/7P/3K3P b - - 0 1");
        position = position.Move(new Move('p', "a7", "a5"));
        position.EnPassant.Should().Be(0);
    }

    [Test]
    public void EnPassant_Should_Be_Set_When_Opponent_Can_Take()
    {
        var position = Position.FromFen("3k4/p7/7P/1P5P/8/8/8/3K4 b - - 0 1");
        position = position.Move(new Move('p', "a7", "a5"));
        position.EnPassant.Should().Be(Squares.A6);
    }
}