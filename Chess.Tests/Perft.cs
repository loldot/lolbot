using Lolbot.Core;

namespace Lolbot.Tests;

public class Perft
{
    const string Position1 = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";
    const string Position2 = "r3k2r/p1ppqpb1/bn2pnp1/3PN3/1p2P3/2N2Q1p/PPPBBPPP/R3K2R w KQkq - ";
    const string Position4 = "r3k2r/Pppp1ppp/1b3nbN/nP6/BBP1P3/q4N2/Pp1P2PP/R2Q1RK1 w kq - 0 1";
    const string Position5 = "rnbq1k1r/pp1Pbppp/2p5/8/2B5/8/PPP1NnPP/RNBQK2R w KQ - 1 8";

    const string Position6 = "2k5/P2bn2P/4B3/5bb1/5BB1/4b3/p2B1N1p/2K5 w - - 0 1";

    const string pos7 = "r3k2r/p1ppqpb1/bn2pnp1/3PN3/1p2P3/1PN2Q1p/P1PBBPPP/R3K2R b KQkq - 0 1";
    const string pos8 = "r3k2r/p1ppqpb1/bn2pn2/3PN1p1/1p2P3/1PN2Q1p/P1PBBPPP/R3K2R w KQkq - 0 2";

    [TestCase(Position1, 1, 20)]
    [TestCase(Position1, 2, 400)]
    [TestCase(Position1, 3, 8_902)]
    [TestCase(Position1, 4, 197_281)]
    [TestCase(Position1, 5, 4_865_609)]

    [TestCase(Position2, 1, 48)]
    [TestCase(Position2, 2, 2_039)]
    [TestCase(Position2, 3, 97_862)]

    [TestCase(Position4, 1, 6)]
    [TestCase(Position4, 2, 264)]
    [TestCase(Position4, 3, 9_467)]

    [TestCase(Position5, 1, 44)]
    [TestCase(Position5, 2, 1_486)]
    [TestCase(Position5, 3, 62_379)]

    [TestCase(Position6, 1, 38)]
    [TestCase(Position6, 2, 1_118)]
    [TestCase(Position6, 3, 37_389)]
    [TestCase(pos7, 1, 42)]
    [TestCase(pos7, 2, 1_964)]
    [TestCase(pos7, 3, 81_066)]
    [TestCase(pos8, 1, 46)]
    [TestCase(pos8, 2, 1_908)]
    public void PerftCounts(string fen, int depth, int expectedCount)
    {
        var perft = GetPerftCounts(Position.FromFen(fen), depth, fen == pos8);
        perft.Should().Be(expectedCount);
    }

    private static int GetPerftCounts(Position position, int remainingDepth = 4, bool print = false)
    {
        var moves = position.GenerateLegalMoves();
        var count = 0;

        if (remainingDepth == 1) return moves.Length;

        if (print) Console.WriteLine($"Depth: {remainingDepth}");

        if ((position.White | position.Black) != position.Occupied)
        {
            Console.WriteLine("Inconsistent position b+w != occ");
            Console.WriteLine(new FenSerializer().ToFenString(position));
        }

        if ((position.WhitePawns | position.WhiteRooks | position.WhiteBishops 
            | position.WhiteKnights | position.WhiteQueens | position.WhiteKing) != position.White)
        {
            Console.WriteLine("Inconsistent position pices != white");
            Console.WriteLine(new FenSerializer().ToFenString(position));
        }

        if ((position.BlackPawns | position.BlackRooks | position.BlackBishops 
            | position.BlackKnights | position.BlackQueens | position.BlackKing) != position.Black)
        {
            Console.WriteLine("Inconsistent position pices != black");
            Console.WriteLine(new FenSerializer().ToFenString(position));
        }

        foreach (var move in moves)
        {
            var posCount = GetPerftCounts(position.Move(move), remainingDepth - 1);
            if (print)
            {
                Console.WriteLine($"{move}: {posCount}");
            }
            count += posCount;
        }
        return count;
    }
}