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

        Bitboards.Debug(
            MovePatterns.BishopAttacks(1ul << 33, game.CurrentPosition.Empty),
            MovePatterns.SquaresBetween[33][60],
            game.CurrentPosition.Checkmask
        );

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

        Bitboards.Debug(
            MovePatterns.BishopAttacks(1ul << 33, game.CurrentPosition.Empty),
            MovePatterns.SquaresBetween[33][60],
            game.CurrentPosition.Checkmask
        );
        moves.Should().Contain(new Move("c6", "b5", "b5", 'B'));
    }

    [Test]
    public void Knight_Should_Not_Capture_Outside_Checkmask()
    {
        var position = Position.FromFen("8/4k3/8/8/1Q2n3/6Q1/2KQ1Q2/8 b - - 0 1");
        var moves = position
            .GenerateLegalMoves(Color.Black, Piece.BlackKnight)
            .ToArray();

        moves.Should().BeEquivalentTo([new Move("e4", "c5"), new Move("e4", "d6")]);

    }

    [Test]
    public void King_Cannot_Capture_Protected_Checker()
    {
        var position = Position.FromFen("3rkr2/4pB2/8/4N3/8/8/8/8 b - - 0 1");
        var moves = position
            .GenerateLegalMoves(Color.Black, Piece.BlackKing)
            .ToArray();
        
        moves.Should().BeEmpty();
    }

    [Test]
    public void Rook_Can_Capture_Checker()
    {
        var position = Position.FromFen("4rrk1/1b3Bp1/1n3q1p/2p1N3/1p6/7P/PP3PP1/R2QR1K1 b - - 0 1");
        var moves = position
            .GenerateLegalMoves(Color.Black, Piece.WhiteRook)
            .ToArray();

        moves.Should().Contain(new Move("f8", "f7", "f7", 'B'));
    }
}