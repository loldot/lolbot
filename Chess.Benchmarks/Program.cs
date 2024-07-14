using Lolbot.Core;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;

var summary = BenchmarkRunner.Run<Perft5Bench>();


public class Perft5Bench()
{
    [Benchmark]
    public void Current()
    {
        GetPerftCounts(new Position(), 5);
    }



    [Benchmark]
    public void Candidate()
    {
        GetPerftCounts(new PositionCand(), 5);
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

    private static int GetPerftCounts(PositionCand position, int remainingDepth = 5)
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


