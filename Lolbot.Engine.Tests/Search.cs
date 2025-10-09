using Lolbot.Core;

namespace Lolbot.Tests;

public class Searching
{
    private static readonly TranspositionTable tt = new TranspositionTable();
    [TestCase("6bk/6pp/7R/5p2/4pP2/4P3/8/Q6K w - - 0 1")]
    [TestCase("q6k/8/4p3/4Pp2/5P2/7r/6PP/6BK b - - 0 1")]
    public void Should_Find_Mate_In_Two(string fen)
    {
        var pos = MutablePosition.FromFen(fen);
        var game = new Game(pos, []);
        var search = new Search(game, tt, [new int[4096], new int[4096]]);
        search.BestMove().Should().BeOneOf([
            new Move('Q', "A1", "a8"),
            new Move('q', "a8", "a1")
        ]);
    }

    [TestCase("4k3/1p6/3PBPK1/8/8/p7/8/7r w - - 2 46", "d7+")]
    public void Should_Solve_EndGames(string fen, string bm)
    {
        var pos = MutablePosition.FromFen(fen);
        var game = new Game(pos, []);
        var search = new Search(game, tt, [new int[4096], new int[4096]]);
        search.BestMove().Should().Be(PgnSerializer.ParseMove(game, bm));
    }

    [Test]
    public void Search_Eval()
    {
        var pos = MutablePosition.FromFen("k7/6p1/8/8/8/8/6PP/6K1 w - - 0 1");
        var game = new Game(pos, []);
        var search = new Search(game, tt, [new int[4096], new int[4096]]);

        var d1Eval = search.EvaluateMove<PvNode>(pos, 1, 1, -9999, 9999);
        var d2Eval = search.EvaluateMove<PvNode>(pos, 2, 1, -9999, 9999);

        d1Eval.Should().Be(d2Eval);
    }
}