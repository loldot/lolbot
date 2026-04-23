namespace Lolbot.Core;

public class SelfPlayDataGenerator
{
    bool isCancelled = false;
    const int moveTime = 50;

    const int QuiesenceMargin = 56;
    const int SearchMargin = 127;

    public void Stop()
    {
        isCancelled = true;
    }

    int gamesPlayed = 0;

    int whiteWins = 0, blackWins = 0, draws = 0;
    int positionsGenerated = 0;
    int scoreBucket10, scoreBucket100, scoreBucketDecided;

    public void Generate(string output)
    {
        using var fs = File.OpenWrite(output);

        int selectedCount = 0;
        var selectedPositions = new (MutablePosition, int score)[8];

        Engine.Init();
        Span<Move> testMoves = new Move[217];
        while (!isCancelled)
        {
            selectedCount = 0;
            var game = Engine.NewGame();
            float result;

            while (!Arbiter.Decide(game, out result))
            {
                var cts = new CancellationTokenSource(moveTime);
                var (selMove, staticEval, score, quiescenceScore) = Engine.SelectMove(game, cts.Token);
                if (selMove is null) break;

                var move = selMove.Value;

                game.Move(in move);


                if (!game.CurrentPosition.IsCheck && MoveGenerator.Captures(game.CurrentPosition, ref testMoves) == 0)
                {
                    if (Math.Abs(score) < (Search.Mate - 1000) && Math.Abs(quiescenceScore - score) < SearchMargin)
                    {
                        if (selectedCount < selectedPositions.Length)
                        {
                            selectedPositions[selectedCount++] = (game.CurrentPosition.Clone(), score);
                        }
                        else
                        {
                            int j = Random.Shared.Next(0, game.HalfMoveCount);
                            if (j < selectedPositions.Length)
                            {
                                selectedPositions[j] = (game.CurrentPosition.Clone(), score);
                            }
                        }
                    }
                }
            }

            foreach (var (pos, score) in selectedPositions.Take(selectedCount))
            {
                int scoreWhite = pos.CurrentPlayer == Colors.White ? -score : score;
                BinarySerializer.WritePosition(fs, pos, (short)scoreWhite, result);

                Console.WriteLine($"{FenSerializer.ToFenString(pos)}|{scoreWhite}|{result}");

                positionsGenerated++;

                if (Math.Abs(scoreWhite) <= 10) scoreBucket10++;
                else if (Math.Abs(scoreWhite) <= 100) scoreBucket100++;
                else scoreBucketDecided++;

                if (result == 1) whiteWins++;
                else if (result == 0) blackWins++;
                else draws++;
            }
            gamesPlayed++;
        }

        Console.WriteLine($"Games played: {gamesPlayed}");
        Console.WriteLine($"Positions generated: {positionsGenerated}");
        Console.WriteLine($"Score distribution: <=10: {scoreBucket10}, <=100: {scoreBucket100}, >100: {scoreBucketDecided}");
        Console.WriteLine($"Results: White wins: {whiteWins}, Black wins: {blackWins}, Draws: {draws}");
        Console.WriteLine("Saved to " + output);

        Environment.Exit(0);
    }
}