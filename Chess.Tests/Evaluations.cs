using Lolbot.Core;

namespace Lolbot.Tests;

public class Evaluations
{
    [Test]
    public void New_Game_Should_Have_EqualPosition()
    {
        var eval = Engine.Evaluate(new Position());
        eval.Should().Be(0);
    }

    [Test]
    public void Capture_Black_Pawn_Should_Give_White_Lead()
    {
        var game = Engine.NewGame();

        game = Engine.Move(game, "E2", "E4");
        game = Engine.Move(game, "D7", "D5");
        game = Engine.Move(game, "E4", "D5");

        var eval = Engine.Evaluate(game.CurrentPosition);
        eval.Should().BeLessThanOrEqualTo(-100);
    }

    [Test]
    public void Should_Not_Get_Mated()
    {
        var position = Position.FromFen("1k6/1pp5/1Q6/8/8/8/8/KR6 b - - 0 1");
        var game = new Game(position, []);
        var bestMove = Engine.BestMove(game);
        bestMove.Should().Be(new Move('p', "c7", "b6", 'Q'));
    }

    [Test]
    public void Should_Find_Easy_Tactic()
    {
        var position = Position.FromFen("3rr1k1/pp3ppp/8/2p1Q3/2P1P3/1P3PPq/P6P/3R1RK1 b - - 0 21");
        var game = new Game(position, []);
        var bestMove = Engine.BestMove(game);
        bestMove.Should().Be(new Move('q', "h3", "f1", 'R'));
    }

    [Test]
    public void Should_Find_Mate_In_One()
    {
        GetBestMove("1k6/1pp5/1q6/5P2/3PQ1P1/2P5/n6r/K1N1R3 b - - 0 1")
            .Should().Be(new Move('q', "b6", "b2"));
    }

    [Test]
    public void Should_Avoid_Getting_Mated()
    {
        GetBestMove("7k/8/6Q1/3p4/8/8/8/2q3RK b - - 0 1")
            .Should().Be(new Move('q', "c1", "g1", 'R'));
    }
    [Test]
    public void Should_Find_Super_Easy_Mate_In_One()
    {
        GetBestMove("4n2K/6Q1/8/8/8/8/8/k5q1 b - - 0 1")
            .Should().Be(new Move('q', "g1", "g7", 'Q'));
    }

    [Test]
    [Explicit]
    public async ValueTask Should_Not_Crash_With_Filled_History()
    {
        using var fs = File.OpenRead("./Testdata/lolbot-lolbot.pgn");

        var pgn = new PgnSerializer();
        var (game, _) = await pgn.Read(fs);

        var ct = new CancellationTokenSource(20_000);
        var bestMove = Engine.BestMove(game, ct.Token);
    }

    [Test]

    public void Should_Find_Win_In_Zugzwang_RK_k_Endgame_1()
    {
        var bestmove = GetBestMove("3k4/8/4K3/2R5/8/8/8/8 w - - 0 1");
        bestmove.Should().BeOneOf(
            new Move('R', "c5", "c1"),
            new Move('R', "c5", "c2"),
            new Move('R', "c5", "c3"),
            new Move('R', "c5", "c4"),
            new Move('R', "c5", "c6")
        );
    }

    [Test]
    public void Should_Find_Win_In_Zugzwang_RK_k_Endgame_2()
    {
        var bestmove = GetBestMove("1k6/7R/2K5/8/8/8/8/8 w - - 0 1");
        // Re7 Rf7 Rg7 Rh1 Rh2 Rh3 Rh4 Rh5 Rh6 Rh8+
        bestmove.Should().BeOneOf(
            new Move('R', "h7", "e7"),
            new Move('R', "h7", "f7"),
            new Move('R', "h7", "g7"),
            new Move('R', "h7", "h1"),
            new Move('R', "h7", "h2"),
            new Move('R', "h7", "h3"),
            new Move('R', "h7", "h4"),
            new Move('R', "h7", "h5"),
            new Move('R', "h7", "h6"),
            new Move('R', "h7", "h8")
        );
    }

    [Test]
    public void Should_Find_Long_Pawn_Promotion()
    {
        var position = Position.FromFen("8/3k4/8/8/3PK3/8/8/8 w - - 0 1");
        var game = new Game(position, []);

        var ct = new CancellationTokenSource(25_000);
        var bestmove = Engine.BestMove(game, ct.Token);

        bestmove.Should().Be(new Move('K', "e4", "d5"));
    }


    private Move? GetBestMove(string fen)
    {
        var position = Position.FromFen(fen);
        var game = new Game(position, []);
        return Engine.BestMove(game);
    }
}