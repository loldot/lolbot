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
        var moves = game.CurrentPosition.GenerateLegalMoves(Color.Black).ToArray();

        moves.Should().HaveCount(5);
        moves.Should().Contain(new Move("c7", "c6"));
    }

    [Test]
    public async Task Should_Find_Capture_Checking_Piece()
    {
        var pgn = @"

1. e4 d5 2. Bb5+ c6 3. Nf3";

        var (game, _) = await new PgnSerializer().Read(new StringReader(pgn));
        var moves = game.CurrentPosition.GenerateLegalMoves(Color.Black).ToArray();

        moves.Should().Contain(new Move("c6", "b5", "b5", 'B'));
    }

    [Test]
    public void Knight_Should_Not_Capture_Outside_Checkmask()
    {
        var position = Position.FromFen("8/4k3/8/8/1Q2n3/6Q1/2KQ1Q2/8 b - - 0 1");
        var moves = position
            .GenerateLegalMoves('n')
            .ToArray(); ;
        moves.Should().BeEquivalentTo([new Move("e4", "c5"), new Move("e4", "d6")]);
    }

    [Test]
    public void King_Cannot_Capture_Protected_Checker()
    {
        var position = Position.FromFen("3rkr2/4pB2/8/4N3/8/8/8/8 b - - 0 1");
        var moves = position
            .GenerateLegalMoves('k')
            .ToArray();
        moves.Should().BeEmpty();
    }

    [Test]
    public void Rook_Can_Capture_Checker()
    {
        var moves = GetLegalMoves("4rrk1/1b3Bp1/1n3q1p/2p1N3/1p6/7P/PP3PP1/R2QR1K1 b - - 0 1");
        moves.Should().Contain(new Move("f8", "f7", "f7", 'B'));
    }

    [Test]
    public void White_Pawns_Should_Check()
    {
        var moves = GetLegalMoves("q3k3/2P2PP1/3K4/8/8/8/8/8 b - - 0 1");
        moves.Should().OnlyContain(x => x == new Move("e8", "f7", "f7", 'P'));
    }

    [Test]
    public void Black_Pawns_Should_Check()
    {
        var moves = GetLegalMoves("8/8/8/8/8/3k4/pp2p3/2K4Q w - - 0 1");
        moves.Should().OnlyContain(x => x == new Move("c1", "b2", "b2", 'p'));
    }

    [Test]
    public void King_Should_Not_Be_Able_To_Move_Along_Check()
    {
        var moves = GetLegalMoves("3k4/5P2/8/Q4B2/8/8/8/8 b - - 0 1");
        moves.Should().OnlyContain(x => x == new Move("d8", "e7"));
    }

    [Test]
    public void Only_King_Can_Move_While_Double_Check()
    {
        var fen = "3k4/1q6/4N3/3Q4/8/8/8/8 b - - 0 1";

        var allMoves = GetLegalMoves(fen);
        var kingMoves = GetLegalMoves(fen, 'k');

        allMoves.Should().BeEquivalentTo(kingMoves);
    }

    private Move[] GetLegalMoves(string fen)
    {
        var position = Position.FromFen(fen);
        return position
            .GenerateLegalMoves()
            .ToArray();
    }

    private Move[] GetLegalMoves(string fen, char piece)
    {
        var position = Position.FromFen(fen);
        return position
            .GenerateLegalMoves(piece)
            .ToArray();
    }
}