namespace Lolbot.Core;

public partial class GenTrainingData
{
    const int SearchDepth = 8;
    const int QuiesenceMargin = 56;
    const int SearchMargin = 87;

    static int[][] history = [new int[4096], new int[4096]];

    public static async Task Generate(string path, string output)
    {
        // Engine.Init();

        // using var inputStream = File.OpenRead(path);
        // using var outputStream = File.OpenWrite(output);

        // int games = 0;
        // int validPositions = 0;
        // int totalPositions = 0;


        // await foreach (var (game, meta) in PgnSerializer.ReadMultiple(inputStream))
        // {
        //     games++;
        //     for (int i = 0; i < history.Length; i++)
        //     {
        //         Array.Clear(history[i]);
        //     }

        //     Console.WriteLine(meta["White"] + " vs " + meta["Black"]);

        //     if (game.GenerateLegalMoves().Length == 0)
        //     {
        //         for (int i = 0; i < SearchDepth && game.Moves.Any(); i++)
        //         {
        //             game.UndoLastMove();
        //         }
        //     }

        //     var search = new Search(game, Engine.tt, history);


        //     foreach (var _ in game.Moves)
        //     {
        //         var staticEval = Heuristics.StaticEvaluation(game.CurrentPosition);
        //         var quiesenceEval = search.QuiesenceSearchPv(game.CurrentPosition, -Search.Inf, Search.Inf);
        //         totalPositions++;

        //         if (!game.CurrentPosition.IsCheck && Math.Abs(staticEval - quiesenceEval) < QuiesenceMargin)
        //         {
        //             search.BestMove(SearchDepth);
        //             var eval = search.CentiPawnEvaluation;

        //             if (Math.Abs(staticEval - eval) < SearchMargin)
        //             {
        //                 float wdl = 1 / (1 + MathF.Exp(-eval / 410f));
        //                 BinarySerializer.WritePosition(outputStream, game.CurrentPosition, (short)eval, wdl);

        //                 validPositions++;

        //                 var mutation = game.CurrentPosition.Clone();
        //                 mutation.DropRandomPiece();

        //                 var mutatedGame = new Game(mutation);
        //                 if (mutatedGame.GenerateLegalMoves().Length > 0)
        //                 {
        //                     var mutEval = Heuristics.StaticEvaluation(mutatedGame.CurrentPosition);
        //                     var mutQuiesenceEval = search.QuiesenceSearchPv(mutatedGame.CurrentPosition, -Search.Inf, Search.Inf);

        //                     if (!mutation.IsCheck && Math.Abs(mutEval - mutQuiesenceEval) < QuiesenceMargin)
        //                     {
        //                         var mutSearch = new Search(mutatedGame, Engine.tt, history);

        //                         mutSearch.BestMove(SearchDepth);

        //                         var mutScore = mutSearch.CentiPawnEvaluation;

        //                         if (Math.Abs(mutEval - mutScore) < SearchMargin)
        //                         {
        //                             float mutWdl = 1 / (1 + MathF.Exp(-mutScore / 410f));
        //                             BinarySerializer.WritePosition(outputStream, mutatedGame.CurrentPosition, (short)mutScore, mutWdl);
        //                             validPositions++;
        //                         }
        //                     }
        //                 }
        //             }
        //         }

        //         game.UndoLastMove();
        //     }
        // }
        // Console.WriteLine($"Processed {games} games: {validPositions}/{totalPositions} valid positions");
    }
}