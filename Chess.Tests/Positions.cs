using Chess.Api;

namespace Chess.Tests;

public class Positions
{
    [Test]
    public void To_FEN_String()
    {
        var position = new Position();
        position.ToPartialFENString().Should().StartWith("rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR");
    }
}