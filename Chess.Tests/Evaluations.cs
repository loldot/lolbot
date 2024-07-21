using Lolbot.Core;

namespace Lolbot.Tests;

public class Evaluations
{
    [Test]
    public void New_Game_Should_Have_EqualPosition()
    {
        var newGame = Engine.NewGame();
        var eval = Engine.Evaluate(newGame.CurrentPosition);
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
        eval.Should().Be(135);
    }

    [Test]
    public void Should_Not_Get_Mated()
    {

        var position = Position.FromFen("1k6/1pp5/1Q6/8/8/8/8/KR6 b - - 0 1");
        var game = new Game(position, []);
        var bestMove = Engine.Reply(game);
        bestMove.Should().Be(new Move('p', "c7", "b6", 'Q'));
    }

    [Test]
    public void Should_Find_Easy_Tactic()
    {
        var position = Position.FromFen("3rr1k1/pp3ppp/8/2p1Q3/2P1P3/1P3PPq/P6P/3R1RK1 b - - 0 21");
        var game = new Game(position, []);
        var bestMove = Engine.Reply(game);
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

    private Move? GetBestMove(string fen)
    {
        var position = Position.FromFen(fen);
        var game = new Game(position, []);
        return Engine.Reply(game);
    }
}