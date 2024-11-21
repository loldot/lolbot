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

        var pos = game.CurrentPosition;
        var eval = Search.StaticEvaluation(in pos);
        eval.Should().BeCloseTo(-100, 10    );
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
    public void Should_Eval_IsolatedPawns()
    {
        var position = Position.FromFen("1k6/p6p/1p1p1p2/8/2P5/5PP1/P3P2P/5K2 w - - 0 1");
        var whiteEval = Heuristics.PawnStructure(position, Colors.White);
        var blackEval = Heuristics.PawnStructure(position, Colors.Black);

        whiteEval.Should().Be(2 * Heuristics.IsolatedPawnPenalty);
        blackEval.Should().Be(3 * Heuristics.IsolatedPawnPenalty);
    }

    [TestCase("8/8/3k4/1Pp1p3/2PpPpp1/3P4/5K2/8 w - - 0 1", Heuristics.PassedPawnBonus, 2 * Heuristics.PassedPawnBonus)]
    [TestCase("8/8/3k4/2p2p2/3p2p1/1PP1PP2/5K2/8 w - - 0 1", 0, 0)]
    public void Should_Eval_PassedPawns(string fen, int white, int black)
    {
        var position = Position.FromFen(fen);

        var whiteEval = Heuristics.PawnStructure(position, Colors.White);
        var blackEval = Heuristics.PawnStructure(position, Colors.Black);

        whiteEval.Should().Be(white);
        blackEval.Should().Be(black);
    }


    [TestCase("3k4/2p1q3/3p2n1/4p3/4P3/3P2N1/2P1Q3/3K4 w - - 0 1", "3k4/2p1q3/3p2n1/4p3/4P3/3P2N1/2P1Q3/3K4 b - - 0 1")]
    [TestCase("3k4/2p5/3p2n1/4p3/4P3/3P2N1/2P1Q3/q2K4 w - - 0 1", "Q2k4/2p1q3/3p2n1/4p3/4P3/3P2N1/2P5/3K4 b - - 0 1")]
    public void Symmetric_Position_Should_Be_Equal(string white, string black)
    {
        var w = Search.StaticEvaluation(Position.FromFen(white));
        var b = Search.StaticEvaluation(Position.FromFen(black));

        w.Should().Be(b);
    }

    [TestCase("3k4/2p5/3p2n1/4p3/4P3/3P2N1/2P1Q3/q2K4 w - - 0 1")]
    [TestCase("Q2k4/2p1q3/3p2n1/4p3/4P3/3P2N1/2P5/3K4 b - - 0 1")]
    public void Equal_Material_Check_Should_Be_Negative(string fen)
    {
        var position = Position.FromFen(fen);
        
        Search.StaticEvaluation(in position)
            .Should().BeNegative();
    }

    [Test]
    public void Promotion_Should_Be_Winning()
    {
        var fen = "2krb3/3p2P1/2p5/8/5P2/4P3/3PK3/8 w - - 0 1";
        Engine.Evaluate(Position.FromFen(fen)).Should().BePositive();
    }

    [Test]
    public void KingSafety_Should_Be_Favorable()
    {
        var fen = "rb1qkr2/ppp2p1p/2np2p1/8/8/2NP2P1/PPP2P1P/RB1Q1RK1 w - - 0 1";
        var eval = Heuristics.KingSafety(Position.FromFen(fen), Colors.White);
        eval.Should().BePositive();
    }

    [Test]
    public void DoubledPawns()
    {
        var fen = "8/8/2k5/2p2p2/5P2/2K2P2/8/8 w - - 0 1";
        var position = Position.FromFen(fen);
        var eval = Heuristics.PawnStructure(position.WhitePawns, position.BlackPawns, Colors.White);
        eval.Should().BeNegative();
    }
}