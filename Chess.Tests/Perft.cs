using Lolbot.Core;

namespace Lolbot.Tests;

public class Perft
{
    [TestCase(1, 20)]
    [TestCase(2, 400)]
    [TestCase(3, 8_902)]
    [TestCase(4, 197_281)]
    [TestCase(5, 4_865_609)]
    public void HasCorrectPerftCounts_FromStartPosition(int depth, int expectedCount)
    {
        var game = new Game();
        var perft = GetPerftCounts(game.CurrentPosition, depth);
        perft.Should().Be(expectedCount);
    }

    private int GetPerftCounts(Position position, int remainingDepth = 4)
    {
        var moves = position.GenerateLegalMoves();
        var count = 0;

        if (remainingDepth == 1) return moves.Length;

        foreach (var move in moves)
        {
            count += GetPerftCounts(position.Move(move), remainingDepth - 1);
        }

        return count;
    }
}