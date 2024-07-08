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
        Bitboards.Debug(pos.Pinmask);
        moves.Length.Should().Be(count);
    }
}