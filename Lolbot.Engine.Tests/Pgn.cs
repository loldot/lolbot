using System.Diagnostics.Contracts;
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
        var (_, meta) = await reader.ReadSingle(pgnFile);

        meta["White"].Should().Be("Fischer, Robert J.");
        meta["Black"].Should().Be("Spassky, Boris V.");
    }

    [Test]
    public async Task CanParseGame()
    {
        var pgnFile = File.OpenRead(@"./Testdata/Fischer-Spassky-92.pgn");
        var reader = new PgnSerializer();
        var (game, _) = await reader.ReadSingle(pgnFile);

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

        game.Moves[47].Should().Be(new Move('r', "f8", "f7", 'B'));
    }

    [Test]
    public async Task CanParseGameWithPromotion()
    {
        var pgnFile = File.OpenRead(@"./Testdata/lichess.pgn");
        var reader = new PgnSerializer();
        var (game, _) = await reader.ReadSingle(pgnFile);

        game.Moves.Should().Contain(Move.Promote('p', "h2", "h1", 'q'));
    }

    [Test]
    public async Task CanParseMultipleGames()
    {
        var pgnFile = File.OpenRead(@"./Testdata/lichess-multi.pgn");
        var games = await PgnSerializer.ReadMultiple(pgnFile).ToListAsync();

        games.Count.Should().Be(17);
    }

    
    [TestCase("./Testdata/lichess-2.pgn")]
    [TestCase("./Testdata/lichess-3.pgn")]
    [TestCase("./Testdata/lichess-4.pgn")]
    public async Task CanParseGamesWithoutError(string gamePath)
    {
        var pgnFile = File.OpenRead(gamePath);
        var reader = new PgnSerializer();
        var (game, _) = await reader.ReadSingle(pgnFile);
    }

    [Test]
    public async Task CanParseGameWhenLongCastleChecks()
    {
        var pgnFile = File.OpenRead(@"./Testdata/Castle-With-Check.pgn");
        var reader = new PgnSerializer();
        var (game, _) = await reader.ReadSingle(pgnFile);
    }

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

    [Test]
    public void Should_Not_Allow_Illegal_Castling()
    {
        var pgn = File.OpenRead(@"./Testdata/castling-bug.pgn");
        var reader = new PgnSerializer();
        Func<Task> act = async () => await reader.ReadSingle(pgn);
        act.Should().ThrowAsync<PgnParseException>().WithMessage("Could not disambiguate move O-O-O");
    }
}