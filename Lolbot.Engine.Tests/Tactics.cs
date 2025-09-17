using Lolbot.Core;

namespace Lolbot.Tests;
public class Tactics
{
    [Test]
    public void Should_Find_Easy_Tactic()
    {
        var position = MutablePosition.FromFen("3rr1k1/pp3ppp/8/2p1Q3/2P1P3/1P3PPq/P6P/3R1RK1 b - - 0 21");
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
        var position = MutablePosition.FromFen("8/3k4/8/8/3PK3/8/8/8 w - - 0 1");
        var game = new Game(position, []);

        var ct = new CancellationTokenSource(5_000);
        var bestmove = Engine.BestMove(game, ct.Token);

        bestmove.Should().Be(new Move('K', "e4", "d5"));
    }

    [Test]
    public void Should_Find_Zugzwang()
    {
        var position = MutablePosition.FromFen("6k1/1rp2b2/1p1p2p1/3P1p1p/2P1p2Q/7R/5PPP/2q2BK1 w - - 0 1");
        var game = new Game(position, []);


        var bestmove = Engine.BestMove(game);
        bestmove.Should().Be(new Move('Q', "h4", "d8"));

        Engine.Move(game, bestmove!.Value);

        bestmove = Engine.BestMove(game);
        Engine.Move(game, bestmove!.Value);

        bestmove = Engine.BestMove(game);
        bestmove.Should().Be(new Move('Q', "d8", "a8"));
    }

    // [Explicit]
    [TestCase("2rqk2r/pb1nbp1p/4p1p1/1B1n4/Np1N4/7Q/PP3PPP/R1B1R1K1 w kq -", "Rxe6")]
    [TestCase("r1bq1rk1/3nbppp/p2pp3/6PQ/1p1BP2P/2NB4/PPP2P2/2KR3R w - -", "Bxg7")]
    [TestCase("2kr4/ppq2pp1/2b1pn2/2P4r/2P5/3BQN1P/P4PP1/R4RK1 b - -", "Ng4")]
    [TestCase("r1bqr1k1/pp1n1ppp/5b2/4N1B1/3p3P/8/PPPQ1PP1/2K1RB1R w - -", "Nxf7")]
    [TestCase("3r4/2r5/p3nkp1/1p3p2/1P1pbP2/P2B3R/2PRN1P1/6K1 b - -", "Rc3")]

    [TestCase("3b4/p3P1q1/P1n2pr1/4p3/2B1n1Pk/1P1R4/P1p3KN/1N6 w - -", "Rh3")]
    [TestCase("7r/8/pB1p1R2/4k2q/1p6/1Pr5/P5Q1/6K1 w - -", "Bd4")]
    [TestCase("3r1r1k/1b4pp/ppn1p3/4Pp1R/Pn5P/3P4/4QP2/1qB1NKR1 w - -", "Rxh7")]
    [TestCase("1k2r2r/pbb2p2/2qn2p1/8/PP6/2P2N2/1Q2NPB1/R4RK1 b - -", "Qxf3")]
    [TestCase("r6k/6R1/p4p1p/2p2P1P/1pq1PN2/6P1/1PP5/2KR4 w - -", "b3")]
    public void CCC_TestPositions_Should_Find_Move(string fen, string best)
    {
        var timer = new CancellationTokenSource(1 * 6_000);

        var position = MutablePosition.FromFen(fen);
        var game = new Game(position, []);
        var bestmove = Engine.BestMove(game, timer.Token);

        bestmove.Should().Be(PgnSerializer.ParseMove(game, best));
    }

    [TestCase("6k1/p3b1np/6pr/6P1/1B2p2Q/K7/7P/8 w - -", "Qxh6")]
    public void CCC_TestPositions_Should_Avoid_Move(string fen, string worst)
    {
        var timer = new CancellationTokenSource(5000);

        var position = MutablePosition.FromFen(fen);
        var game = new Game(position, []);
        var bestmove = Engine.BestMove(game, timer.Token);

        bestmove.Should().NotBe(PgnSerializer.ParseMove(game, worst));
    }


    [Test]
    public void Should_Win_Easy_EndGame()
    {
        var bm = GetBestMove("8/1p4p1/8/4kp1p/1p5P/1K6/5PP1/8 b - - 0 1");
        bm.Should().BeOneOf([
            new Move('p', "f5", "f4"),
            new Move('k', "e5", "d4"),
            new Move('k', "e5", "d6"),
            new Move('k', "e5", "e4"),
            new Move('k', "e5", "f4"),
        ]);
    }

    [Test]
    public void Should_Not_Get_Mated()
    {
        var position = MutablePosition.FromFen("1k6/1pp5/1Q6/8/8/8/8/KR6 b - - 0 1");
        var game = new Game(position, []);
        var bestMove = Engine.BestMove(game);
        bestMove.Should().Be(new Move('p', "c7", "b6", 'Q'));
    }


    private static Move? GetBestMove(string fen)
    {
        var position = MutablePosition.FromFen(fen);
        var game = new Game(position, []);
        return Engine.BestMove(game);
    }
}