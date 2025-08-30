using Lolbot.Core;

namespace Lolbot.Tests;

[Category(TestSuites.Fast)]
public class Positions
{
    [Test]
    public void To_FEN_String()
    {
        FenSerializer.ToFenString(new MutablePosition())
            .Should().StartWith("rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR");
    }

    [Test]
    public void FromArray_Should_Set_A1_Squares()
    {
        var a1 = Bitboards.Create([
            0,0,0,0,0,0,0,0,
            0,0,0,0,0,0,0,0,
            0,0,0,0,0,0,0,0,
            0,0,0,0,0,0,0,0,
            0,0,0,0,0,0,0,0,
            0,0,0,0,0,0,0,0,
            0,0,0,0,0,0,0,0,
            1,0,0,0,0,0,0,0,
        ]);

        a1.Should().Be(1);
    }

    [Test]
    public void FromArray_Should_Set_Squares()
    {
        var occupiedAtStart = Bitboards.Create([
            1,1,1,1,1,1,1,1,
            1,1,1,1,1,1,1,1,
            0,0,0,0,0,0,0,0,
            0,0,0,0,0,0,0,0,
            0,0,0,0,0,0,0,0,
            0,0,0,0,0,0,0,0,
            1,1,1,1,1,1,1,1,
            1,1,1,1,1,1,1,1,
        ]);

        occupiedAtStart.Should().Be(new MutablePosition().Occupied);
    }
}