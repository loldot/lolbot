using Lolbot.Core;

namespace Lolbot.Tests;

[Category(TestSuites.Fast)]
public class PgnEdgeCases
{
    [Test]
    public void Parse_Disambiguation_File_And_Rank()
    {
    // Two knights (g3 and d2) can move to e4; disambiguate by file and by rank
    var pos = MutablePosition.FromFen("4k3/8/8/8/8/6N1/3N4/4K3 w - - 0 1");
        var game = new Game(pos, []);

    var moveFile = PgnSerializer.ParseMove(game, "Nge4");
    moveFile.FromIndex.Should().Be(Squares.G3);
    moveFile.ToIndex.Should().Be(Squares.E4);

    var moveRank = PgnSerializer.ParseMove(game, "N2e4");
    moveRank.FromIndex.Should().Be(Squares.D2);
    moveRank.ToIndex.Should().Be(Squares.E4);
    }

    [Test]
    public void Parse_Castling_Tokens_OO_and_00()
    {
        // White castle
        var whiteCastle = MutablePosition.FromFen("r3k2r/8/8/8/8/8/8/R3K2R w KQkq - 0 1");
        var g1 = new Game(whiteCastle, []);
        var wCastle = PgnSerializer.ParseMove(g1, "O-O");
        wCastle.CastleIndex.Should().NotBe(0);

        // Black castle
        var blackCastle = MutablePosition.FromFen("r3k2r/8/8/8/8/8/8/R3K2R b KQkq - 0 1");
        var g2 = new Game(blackCastle, []);
        var bCastle = PgnSerializer.ParseMove(g2, "0-0");
        bCastle.CastleIndex.Should().NotBe(0);
    }
}
