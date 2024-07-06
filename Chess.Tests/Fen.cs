using Lolbot.Core;
namespace Lolbot.Tests;

public class Fen
{
    [Test]
    public void Parse_Should_Have_Correct_Piece_Placements()
    {
        var fenString = "8/5k2/8/8/2Q2n2/8/6P1/8 b - - 0 1";
        var serializer = new FenSerializer();
        var position = serializer.Parse(fenString);

        position.WhiteQueens.Should().Be(Bitboards.Create("c4"));
        position.WhitePawns.Should().Be(Bitboards.Create("g2"));
        position.BlackKing.Should().Be(Bitboards.Create("f7"));
        position.BlackKnights.Should().Be(Bitboards.Create("f4"));
    }

    [Test]
    public void ShouldSetCurrentPlayer()
    {
        var fenString = "r3k2r/ppp3pp/8/8/8/8/P5PP/R3K2R b KQk - 0 1";
        var serializer = new FenSerializer();
        var position = serializer.Parse(fenString);

        position.CurrentPlayer.Should().Be(Color.Black);
    }

    [Test]
    public void Should_Set_CastlingRights()
    {
        var fenString = "r3k2r/ppp3pp/8/8/8/8/P5PP/R3K2R b KQk - 0 1";
        var serializer = new FenSerializer();
        var position = serializer.Parse(fenString);

        position.CastlingRights.Should().Be(CastlingRights.WhiteKing | CastlingRights.WhiteQueen | CastlingRights.BlackKing);
    }

    [Test]
    public void Should_Set_EnPassant_Square()
    {
        var fenString = "4k3/2q5/8/3pP3/8/8/8/3KQ3 w - d6 0 1";
        var serializer = new FenSerializer();
        var position = serializer.Parse(fenString);

        position.EnPassant.Should().Be(Squares.IndexFromCoordinate("d6"));
    }
}