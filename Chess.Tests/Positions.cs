using Lolbot.Core;

namespace Lolbot.Tests;

[Category(TestSuites.Fast)]
public class Positions
{
    [Test]
    public void To_FEN_String()
    {
        var fen = new FenSerializer();
        fen.ToFenString(new Position())
            .Should().StartWith("rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR");
    }

    [Test]
    public void FromArray_Should_Set_A1_Squares()
    {
        var a1 = Bitboards.Create((int[])([
            0,0,0,0,0,0,0,0,
            0,0,0,0,0,0,0,0,
            0,0,0,0,0,0,0,0,
            0,0,0,0,0,0,0,0,
            0,0,0,0,0,0,0,0,
            0,0,0,0,0,0,0,0,
            0,0,0,0,0,0,0,0,
            1,0,0,0,0,0,0,0,
        ]));

        a1.Should().Be(1);
    }

    [Test]
    public void FromArray_Should_Set_Squares()
    {
        var occupiedAtStart = Bitboards.Create((int[])([
            1,1,1,1,1,1,1,1,
            1,1,1,1,1,1,1,1,
            0,0,0,0,0,0,0,0,
            0,0,0,0,0,0,0,0,
            0,0,0,0,0,0,0,0,
            0,0,0,0,0,0,0,0,
            1,1,1,1,1,1,1,1,
            1,1,1,1,1,1,1,1,
        ]));

        occupiedAtStart.Should().Be(new Position().Occupied);
    }
}