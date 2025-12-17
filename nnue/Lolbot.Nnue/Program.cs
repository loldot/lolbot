using Lolbot.Core;

if (args.Length != 1)
{
    Console.WriteLine("Usage: Lolbot.Nnue <directory>");
    return;
}

var dir = args[0];

foreach(var file in Directory.GetFiles(dir, "*.pgn"))
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