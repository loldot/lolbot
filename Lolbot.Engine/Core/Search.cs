using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using static System.Math;

namespace Lolbot.Core;

public sealed class Search(Game game, TranspositionTable tt, int[][] historyHeuristic)
{
    private const int Max_Depth = 64;
    private readonly MutablePosition rootPosition = game.CurrentPosition;
    private readonly RepetitionTable history = game.RepetitionTable;


    private CancellationToken ct;

    public Action<SearchProgress>? OnSearchProgress { get; set; }
    public int CentiPawnEvaluation { get; private set; }


    static Search()
    {

    }

    public Move BestMove()
    {
        var timer = new CancellationTokenSource(2_000);
        return BestMove(timer.Token);
    }
    public Move BestMove(CancellationToken ct)
    {
        this.ct = ct;
        return DoSearch(Max_Depth, ct);
    }

    public Move BestMove(int searchDepth)
    {
        this.ct = new CancellationTokenSource(60_000).Token;
        return DoSearch(searchDepth, ct);
    }

    public Move DoSearch(int maxSearchDepth, CancellationToken ct)
    {
        this.ct = ct;
        return Move.Null;
    }

}

public record SearchProgress(int Depth, Move BestMove, int Eval, int Nodes, double Time)
{
}