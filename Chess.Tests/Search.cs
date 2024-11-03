using Lolbot.Core;

namespace Lolbot.Tests;

public class Searching
{

    [TestCase("6bk/6pp/7R/5p2/4pP2/4P3/8/Q6K w - - 0 1")]
    [TestCase("q6k/8/4p3/4Pp2/5P2/7r/6PP/6BK b - - 0 1")]
    public void Should_Find_Mate_In_Two(string fen)
    {
        var pos = Position.FromFen(fen);
        var game = new Game(pos, []);
        var search = new Search(game);
        search.BestMove().Should().BeOneOf([
            new Move('Q', "A1", "a8"),
            new Move('q', "a8", "a1")
        ]);
    }
}