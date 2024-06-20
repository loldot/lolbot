using Lolbot.Core;

namespace Lolbot.Tests;

public class Checks
{
    [Test]
    public async Task Should_Find_Evasions()
    {
        var pgn = @"

1. e4 d5 2. Bb5+";

        var (game, _) = await new PgnSerializer().Read(new StringReader(pgn));
        var moves = game.CurrentPosition.GenerateLegalMoves(Color.Black);

        Bitboards.Debug(game.CurrentPosition.Checkmask);
        moves.Should().HaveCount(5);
        moves.Should().Contain(new Move("c7", "c6"));
    }

}