using Lolbot.Core;

namespace Lolbot.Tests;

[Category(TestSuites.Fast)]
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

        Move[] expectedMoves = [
            new Move('P', "e2", "e4"),
            new Move('p', "e7", "e5"),
            new Move('N', "g1", "f3"),
            new Move('n', "b8", "c6"),
            new Move('B', "f1", "b5"),
            new Move('p', "a7", "a6"),
            new Move('B', "b5", "a4")
        ];

        game.Moves[0..expectedMoves.Length].Should().BeEquivalentTo(expectedMoves);
        game.Moves[19].Should().Be(new Move('n', "b8", "d7"));
        game.Moves[22].Should().Be(new Move('P', "c4", "b5", 'p'));

        game.Moves[47].Should().Be(new Move('r',"f8","f7",'B'));
    }
}