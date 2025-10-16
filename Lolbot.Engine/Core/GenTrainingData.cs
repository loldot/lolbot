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

    public static async Task Generate(string path, string output)
    {
        Engine.Init();

        using var inputStream = File.OpenRead(path);
        using var outputStream = File.OpenWrite(output);

        int max_depth = 8;
        int games = 0;
        int validPositions = 0;
        int totalPositions = 0;


        await foreach (var (game, meta) in PgnSerializer.ReadMultiple(inputStream))
        {
            games++;
            for (int i = 0; i < history.Length; i++)
            {
                Array.Clear(history[i]);
            }

            Console.WriteLine(meta["White"] + " vs " + meta["Black"]);

            if (game.IsCheckMate() || game.IsStaleMate())
            {
                for (int i = 0; i < max_depth; i++)
                {
                    game.UndoLastMove();
                }
            }

            var search = new Search(game, Engine.tt, history);


            foreach (var _ in game.Moves)
            {
                var staticEval = Heuristics.StaticEvaluation(game.CurrentPosition);
                var quiesenceEval = search.QuiesenceSearch(game.CurrentPosition, -Search.Inf, Search.Inf);
                totalPositions++;

                if (Math.Abs(staticEval - quiesenceEval) < QuiesenceMargin)
                {

                    var (_, eval) = search.BestMove(max_depth);

                    if (Math.Abs(staticEval - eval) < SearchMargin)
                    {
                        float wdl = 1 / (1 + MathF.Exp(-eval / 410f));
                        BinarySerializer.WritePosition(outputStream, game.CurrentPosition, (short)eval, wdl);

                        validPositions++;
                    }
                }

                game.UndoLastMove();
            }
        }
        Console.WriteLine($"Processed {games} games: {validPositions}/{totalPositions} valid positions");
    }
}