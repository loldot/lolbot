using System.Runtime.ConstrainedExecution;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Lolbot.Core;

public partial class GenTrainingData
{
    const int QuiesenceMargin = 56;
    const int SearchMargin = 87;

    public static (int staticEval, int nnueEval, int score, bool isValid) TestPosition(Search search, MutablePosition position)
    {
        var staticEval = Heuristics.StaticEvaluation(position);
        var quiesenceEval = search.QuiesenceSearch(position, -Search.Inf, Search.Inf);
        var score = search.EvaluateMove<PvNode>(position, 7, 0, -Search.Inf, Search.Inf, true);

        var isValid = position.IsEndgame ||
            (Math.Abs(staticEval - quiesenceEval) < QuiesenceMargin &&
             Math.Abs(staticEval - score) < SearchMargin);
        var nnueEval = position.Eval;
        (staticEval, nnueEval, score) = position.CurrentPlayer == Colors.White
            ? (staticEval, nnueEval, score) 
            : (-staticEval, -nnueEval, -score);
        return (staticEval, nnueEval, score, isValid);
    }

    static int[][] history = [new int[4096], new int[4096]];

    public sealed class GenOptions
    {
        public int MaxPositions { get; set; } = 50_000_000;

        public int Start { get; set; } = 0;
        public int End { get; set; } = 50_000_000;

        public bool RandomSample { get; set; } = false;
    }
    public static async IAsyncEnumerable<(MutablePosition, int)> Generate(string path, GenOptions options)
    {
        string? line;
        using var fs = File.OpenRead(path);
        using var reader = new StreamReader(fs);

        int readCount = 0;
        int sampled = 0;

        while (readCount < options.End && (line = await reader.ReadLineAsync()) != null)
        {
            if (readCount < options.Start)
            {
                readCount++;
                continue;
            }

            if (options.RandomSample && sampled >= options.MaxPositions)
            {
                yield break;
            }

            if (options.RandomSample && Random.Shared.NextDouble() > (options.MaxPositions - sampled) / (double)(options.End - readCount))
            {
                readCount++;
                continue;
            }

            line = line[9..]; // skip {"fen": "}

            var fenEnd = line.IndexOf('"');
            var fen = line[..fenEnd];
            var sfEval = int.Parse(DigitRegex().Match(line[fenEnd..]).Value);

            var position = MutablePosition.FromFen(fen);
            if (position.IsCheck || position.CurrentPlayer == Colors.Black)
            {
                readCount++;
                continue;
            }

            var game = new Game(position, []);

            var search = new Search(game, Engine.tt, history);

            var (stat, nnue, score, isValid) = TestPosition(search, position);
            if (Math.Sign(score) != Math.Sign(sfEval) && Math.Abs(score - sfEval) > SearchMargin)
            {
                isValid = false;
            }

            if (isValid)
            {
                sampled++;
                yield return (position, score);
            }
            else
            {
                continue;
            }

            Console.WriteLine($"{fen} | {stat} | {nnue} | {score} | {sfEval}");

            readCount++;
        }
    }

    [GeneratedRegex(@"-?\d+")]
    private static partial Regex DigitRegex();
}