using Lolbot.Core;

namespace Lolbot.Tests;

public class Pgn
{
    [Test]
    public async Task CanParseMetadata()
    {
        var pgnFile = File.OpenRead(@"./Testdata/Fischer-Spassky-92.pgn");
        var reader = new PgnSerializer();
        var (_, meta) = await reader.Read(pgnFile);

        meta["White"].Should().Be("Fischer, Robert J.");
        meta["Black"].Should().Be("Spassky, Boris V.");
    }

    [Test]
    public async Task CanParseGame()
    {
        var pgnFile = File.OpenRead(@"./Testdata/Fischer-Spassky-92.pgn");
        var reader = new PgnSerializer();
        var (game, _) = await reader.Read(pgnFile);

        game.Moves[0].Should().Be(new Move("e2", "e4"));
        game.Moves[2].Should().Be(new Move("g1", "f3"));
    }
}