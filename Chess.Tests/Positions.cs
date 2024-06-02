using Chess.Api;

namespace Chess.Tests;

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
        var a1 = Utils.FromArray([
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
        var occupiedAtStart = Utils.FromArray([
            1,1,1,1,1,1,1,1,
            1,1,1,1,1,1,1,1,
            0,0,0,0,0,0,0,0,
            0,0,0,0,0,0,0,0,
            0,0,0,0,0,0,0,0,
            0,0,0,0,0,0,0,0,
            1,1,1,1,1,1,1,1,
            1,1,1,1,1,1,1,1,
        ]);

        occupiedAtStart.Should().Be(new Position().Occupied);
    }
}