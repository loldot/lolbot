using Lolbot.Core;
using BenchmarkDotNet.Attributes;

GetPerftCounts(new Position(), 5);

//var summary = BenchmarkRunner.Run<Perft5Bench>();

static int GetPerftCounts(Position position, int remainingDepth = 4)
{
    Span<Move> moves = new Move[218];
    var currentCount = MoveGenerator.Legal(ref position, ref moves);
    var count = 0;

    if (remainingDepth == 1) return currentCount;

    for (int i = 0; i < currentCount; i++)
    {
        var posCount = GetPerftCounts(position.Move(moves[i]), remainingDepth - 1);
        count += posCount;
    }
    return count;
}

public class Perft5Bench()
{
    [Benchmark]
    public void Current()
    {
        GetPerftCounts(new Position(), 5);
    }




    private static int GetPerftCounts(Position position, int remainingDepth = 5)
    {
        var moves = position.GenerateLegalMoves();
        var count = 0;

        if (remainingDepth == 1) return moves.Length;

        foreach (var move in moves)
        {
            var posCount = GetPerftCounts(position.Move(move), remainingDepth - 1);
            count += posCount;
        }
        return count;
    }
}
