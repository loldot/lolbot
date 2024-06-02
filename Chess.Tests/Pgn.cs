using Chess.Api;

namespace Chess.Tests;

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

        game.Moves[0].Should().Be(new Move("E2", "E4"));
    }
}