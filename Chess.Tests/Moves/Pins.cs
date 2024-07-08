using Lolbot.Core;

namespace Lolbot.Tests;

public class Pins
{
    [TestCase("3k4/3r4/8/8/3R4/8/8/3K4 w - - 0 1", 5)]
    [TestCase("4k3/8/5q2/8/3R4/8/8/K7 w - - 0 1", 0)]
    [TestCase("4k3/8/5q2/8/3N4/2R5/8/K7 w - - 0 1", 14)]
    public void Should_Pin_Rook(string fen, int count)
    {
        var pos = Position.FromFen(fen);
        var moves = pos.GenerateLegalMoves('R');
        moves.Length.Should().Be(count);
    }

    [TestCase("4k3/8/5q2/8/8/2B5/8/K7 w - - 0 1", 4)]
    [TestCase("4k3/8/5q2/6B1/8/2B5/8/K7 w - - 0 1", 11)]
    public void Should_Pin_Bishop(string fen, int count)
    {
        var pos = Position.FromFen(fen);
        var moves = pos.GenerateLegalMoves('B');
        moves.Length.Should().Be(count);
    }

    [Test]
    public void Not_Pinned_When_X_Ray()
    {
        var pos = Position.FromFen("8/K1R1n1rk/8/8/8/8/8/8 w - - 0 1");
        var moves = pos.GenerateLegalMoves('R');

        var notPinned = MovePatterns.RookAttacks(Squares.C7, pos.Occupied) & ~pos.White;
        moves.Length.Should().Be(Bitboards.CountOccupied(notPinned));
    }

    [Test]
    public void Pinned_With_Multiple_Pieces_Attacking_On_Pinmask()
    {
        var pos = Position.FromFen("3q4/K1R2r1k/8/3nb3/8/8/8/8 w - - 0 1");
        var moves = pos.GenerateLegalMoves('R');

        var pinnedMoves = MovePatterns.RookAttacks(Squares.C7, pos.Occupied) & ~pos.White & Bitboards.Masks.GetRank(Squares.C7);
        moves.Length.Should().Be(Bitboards.CountOccupied(pinnedMoves));
    }

    [Test]
    public void Pinned_En_Passant()
    {
        var pos = Position.FromFen("2Q5/1K6/8/2pP4/4q3/5k2/8/8 w - c6 0 1");
        var moves = pos.GenerateLegalMoves('P').ToArray();
        moves.Should().OnlyContain(x => x.CaptureIndex == Squares.C5 && x.ToIndex == Squares.C6);
    }
}