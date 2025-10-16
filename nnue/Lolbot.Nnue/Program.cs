using Lolbot.Core;

foreach(var file in Directory.GetFiles("C:\\dev\\chess-data\\Lichess Elite Database\\Lichess Elite Database", "*.pgn"))
{
    var output = $"{file}.evals.bin";
    if (File.Exists(output))
    {
        Console.WriteLine($"Skipping {file}");
        continue;
    }
    
    await GenTrainingData.Generate(file, output);

    Console.WriteLine($"Processed {file}");
}