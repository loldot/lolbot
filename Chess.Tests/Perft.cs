using Lolbot.Core;

namespace Lolbot.Tests;

public class Perft
{
    const string Position1 = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";
    const string Position2 = "r3k2r/p1ppqpb1/bn2pnp1/3PN3/1p2P3/2N2Q1p/PPPBBPPP/R3K2R w KQkq - ";
    const string Position4 = "r3k2r/Pppp1ppp/1b3nbN/nP6/BBP1P3/q4N2/Pp1P2PP/R2Q1RK1 w kq - 0 1";
    const string Position5 = "rnbq1k1r/pp1Pbppp/2p5/8/2B5/8/PPP1NnPP/RNBQK2R w KQ - 1 8";


    [TestCase(Position1, 1,      20)]
    [TestCase(Position1, 2,     400)]
    [TestCase(Position1, 3,   8_902)]
    [TestCase(Position1, 4, 197_281)]
    // [TestCase(Position1, 5, 4_865_609)]
    
    [TestCase(Position2, 1,      48)]
    [TestCase(Position2, 2,   2_039)]
    [TestCase(Position2, 3,  97_862)]
    
    [TestCase(Position4, 1,       6)]
    [TestCase(Position4, 2,     264)]
    [TestCase(Position4, 3,   9_467)]

    [TestCase(Position5, 1,      44)]
    [TestCase(Position5, 2,   1_486)]
    [TestCase(Position5, 3,  62_379)]

    public void PerftCounts(string fen, int depth, int expectedCount)
    {
        var perft = GetPerftCounts(Position.FromFen(fen), depth);
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