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
        var quiesenceEval = search.QuiesenceSearchPv(position, -Search.Inf, Search.Inf);
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

            if (game.GenerateLegalMoves().Length == 0)
            {
                for (int i = 0; i < max_depth && game.Moves.Any(); i++)
                {
                    game.UndoLastMove();
                }
            }

            var search = new Search(game, Engine.tt, history);


            foreach (var _ in game.Moves)
            {
                var staticEval = Heuristics.StaticEvaluation(game.CurrentPosition);
                var quiesenceEval = search.QuiesenceSearchPv(game.CurrentPosition, -Search.Inf, Search.Inf);
                totalPositions++;

                if (!game.CurrentPosition.IsCheck && Math.Abs(staticEval - quiesenceEval) < QuiesenceMargin)
                {

                    var (_, eval) = search.BestMove(max_depth);

                    if (Math.Abs(staticEval - eval) < SearchMargin)
                    {
                        float wdl = 1 / (1 + MathF.Exp(-eval / 410f));
                        BinarySerializer.WritePosition(outputStream, game.CurrentPosition, (short)eval, wdl);
                        Console.WriteLine("FEN: " + FenSerializer.ToFenString(game.CurrentPosition));

                        validPositions++;

                        var mutation = game.CurrentPosition.Clone();
                        mutation.DropRandomPiece();
                        var mutatedGame = new Game(mutation);
                        if (mutatedGame.GenerateLegalMoves().Length > 0)
                        {
                            var mutEval = Heuristics.StaticEvaluation(mutatedGame.CurrentPosition);
                            var mutQuiesenceEval = search.QuiesenceSearchPv(mutatedGame.CurrentPosition, -Search.Inf, Search.Inf);

                            if (!mutation.IsCheck && Math.Abs(mutEval - mutQuiesenceEval) < QuiesenceMargin)
                            {
                                var mutSearch = new Search(mutatedGame, Engine.tt, history);
                                var (_, mutScore) = mutSearch.BestMove(max_depth);

                                if (Math.Abs(mutEval - mutScore) < SearchMargin)
                                {
                                    float mutWdl = 1 / (1 + MathF.Exp(-mutScore / 410f));
                                    BinarySerializer.WritePosition(outputStream, mutatedGame.CurrentPosition, (short)mutScore, mutWdl);
                                    Console.WriteLine("FEN MUT: " + FenSerializer.ToFenString(mutatedGame.CurrentPosition));
                                    validPositions++;
                                }
                            }
                        }
                    }
                }

                game.UndoLastMove();
            }
        }
        Console.WriteLine($"Processed {games} games: {validPositions}/{totalPositions} valid positions");
    }
}