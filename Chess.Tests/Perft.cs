using Lolbot.Core;

namespace Lolbot.Tests;

public class Perft
{
    const string Position1 = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";
    const string Position2 = "r3k2r/p1ppqpb1/bn2pnp1/3PN3/1p2P3/2N2Q1p/PPPBBPPP/R3K2R w KQkq - ";
    const string Position4 = "r3k2r/Pppp1ppp/1b3nbN/nP6/BBP1P3/q4N2/Pp1P2PP/R2Q1RK1 w kq - 0 1";
    const string Position5 = "rnbq1k1r/pp1Pbppp/2p5/8/2B5/8/PPP1NnPP/RNBQK2R w KQ - 1 8";

    const string Position6 = "2k5/P2bn2P/4B3/5bb1/5BB1/4b3/p2B1N1p/2K5 w - - 0 1";
    

    [TestCase(Position1, 1,        20)]
    [TestCase(Position1, 2,       400)]
    [TestCase(Position1, 3,     8_902)]
    [TestCase(Position1, 4,   197_281)]
    [TestCase(Position1, 5, 4_865_609)]
    
    [TestCase(Position2, 1,      48)]
    [TestCase(Position2, 2,   2_039)]
    [TestCase(Position2, 3,  97_862)]
    
    [TestCase(Position4, 1,       6)]
    [TestCase(Position4, 2,     264)]
    [TestCase(Position4, 3,   9_467)]

    [TestCase(Position5, 1,      44)]
    [TestCase(Position5, 2,   1_486)]
    [TestCase(Position5, 3,  62_379)]

    [TestCase(Position6, 1,    38)]
    [TestCase(Position6, 2, 1_118)]
    [TestCase(Position6, 2, 37_389)]
   
    // 2. h2h3 moves =     181044 h2h3: 181044
    // 2. h2h4 moves =     218829 h2h4: 218829
    // 2. g2g3 moves =     217210 g2g3: 217210
    // 2. g2g4 moves =     214048 g2g4: 214048
    // 2. f2f3 moves =     178889 f2f3: 178889
    // 2. f2f4 moves =     198473 f2f4: 198474 <--
    // 2. e2e3 moves =     402988 e2e3: 402988
    // 2. e2e4 moves =     405385 e2e4: 405385
    // 2. d2d3 moves =     328511 d2d3: 328511
    // 2. d2d4 moves =     361790 d2d4: 361790
    // 2. c2c3 moves =     222861 c2c3: 222861
    // 2. c2c4 moves =     240082 c2c4: 240082
    // 2. b2b3 moves =     215255 b2b3: 215255
    // 2. b2b4 moves =     216145 b2b4: 216145
    // 2. a2a3 moves =     181046 a2a3: 181046
    // 2. a2a4 moves =     217832 a2a4: 217832
    // 2. g1f3 moves =     233491 g1f3: 233491
    // 2. g1h3 moves =     198502 g1h3: 198502
    // 2. b1a3 moves =     198572 b1a3: 198572
    // 2. b1c3 moves =     234656 b1c3: 234656

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
            var posCount = GetPerftCounts(position.Move(move), remainingDepth - 1);
            if(remainingDepth == 5) Console.WriteLine($"{move}: {posCount}");
            count += posCount;
        }

        return count;
    }
}