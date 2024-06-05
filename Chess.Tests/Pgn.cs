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

        Move[] expectedMoves = [
            new Move("e2", "e4"),
            new Move("e7", "e5"),
            new Move("g1", "f3"),
            new Move("b8", "c6"),
            new Move("f1", "b5"),
            new Move("a7", "a6"),
            new Move("b5", "a4")
        ];

        game.Moves[0..expectedMoves.Length].Should().BeEquivalentTo(expectedMoves);
        game.Moves[19].Should().Be(new Move("b8", "d7"));
        game.Moves[22].Should().Be(new Move(
            Squares.IndexFromCoordinate("c4"), 
            Squares.IndexFromCoordinate("b5"),
            Squares.IndexFromCoordinate("b5"),
            Piece.BlackPawn
        ));

        game.Moves[47].Should().Be(new Move(
            Squares.IndexFromCoordinate("f8"), 
            Squares.IndexFromCoordinate("f7"),
            Squares.IndexFromCoordinate("f7"),
            Piece.WhiteBishop
        ));
    }
}