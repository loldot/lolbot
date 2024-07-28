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
    public void Parse_Should_Have_Correct_Piece_Placements_2()
    {
        var fenString = "8/4k3/8/8/1Q2n3/6Q1/2KQ1Q2/8 b - - 0 1";
        var serializer = new FenSerializer();
        var position = serializer.Parse(fenString);

        position.WhiteQueens.Should().Be(Bitboards.Create("b4", "d2", "f2", "g3"));
        position.WhiteKing.Should().Be(Bitboards.Create("c2"));
        position.BlackKing.Should().Be(Bitboards.Create("e7"));
        position.BlackKnights.Should().Be(Bitboards.Create("e4"));
    }

    [Test]
    public void ShouldSetCurrentPlayer()
    {
        var fenString = "r3k2r/ppp3pp/8/8/8/8/P5PP/R3K2R b KQk - 0 1";
        var serializer = new FenSerializer();
        var position = serializer.Parse(fenString);

        position.CurrentPlayer.Should().Be(Color.Black);
    }

    [TestCase("r3k2r/ppp3pp/8/8/8/8/P5PP/R3K2R b KQk - 0 1", CastlingRights.WhiteKing | CastlingRights.WhiteQueen | CastlingRights.BlackKing)]
    [TestCase("r3k2r/p1ppqpb1/bn2pnp1/3PN3/4P3/2N2Q1p/PPpBBPPP/1K1R3R w kq - 0 1", CastlingRights.BlackKing | CastlingRights.BlackQueen)]
    public void Should_Set_CastlingRights(string fen, CastlingRights expectedCastlingRights)
    {
        var serializer = new FenSerializer();
        var position = serializer.Parse(fen);

        position.CastlingRights.Should().Be(expectedCastlingRights);
    }

    [Test]
    public void Should_Set_EnPassant_Square()
    {
        var fenString = "4k3/2q5/8/3pP3/8/8/8/3KQ3 w - d6 0 1";
        var serializer = new FenSerializer();
        var position = serializer.Parse(fenString);

        position.EnPassant.Should().Be(Squares.IndexFromCoordinate("d6"));
    }

    [Test]
    public void Should_Set_Checkmask()
    {
        var pos = Position.FromFen("8/4k3/8/8/1Q2n3/6Q1/2KQ1Q2/8 b - - 0 1");
        var b4 = Squares.IndexFromCoordinate("b4");
        var e7 = Squares.IndexFromCoordinate("e7");
        pos.Checkmask.Should().Be(MovePatterns.SquaresBetween[b4][e7] | Squares.FromCoordinates("b4"));
    }
}