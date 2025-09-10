using System.Diagnostics;
using Lolbot.Core;
using NumSharp;

string file = args.Length > 0 ? args[0] : "";
if (string.IsNullOrEmpty(file))
{
    Console.WriteLine("Usage: Lolbot.Nnue <path to nnue file>");
    Environment.Exit(0);
}

if (!File.Exists(file))
{
    Console.WriteLine($"File not found: {file}");
    Environment.Exit(0);
}
var parent = Path.GetDirectoryName(file);
var output = Path.Combine(parent!, "dataset.csv");

Engine.Init();

const int rowSize = 770;
const int bufferSize = 10000;
var buffer = new List<string>(bufferSize);
// int alreadyHave = 0;
// if (File.Exists(output))
// {
//     // Count lines to resume
//     alreadyHave = File.ReadLines(output).Count();
//     Console.WriteLine($"Resuming from {alreadyHave} rows.");
// }

var row = new float[rowSize];
int generated = 0;
var opts = new GenTrainingData.GenOptions
{
    Start = 0,
    End = 50_000_000,
    MaxPositions = 20_000_000,
    RandomSample = false
};
await foreach (var (pos, eval) in GenTrainingData.Generate(file, opts))
{
    for (int piece = 0; piece < 6; piece++)
    {
        var p = (PieceType)(piece + 1);
        var pw = pos[Colors.White, p];
        var pb = pos[Colors.Black, p];

        while (pb != 0)
        {
            var sq = Bitboards.PopLsb(ref pb);
            row[piece * 64 + sq] = 1;
        }
        while (pw != 0)
        {
            var sq = Bitboards.PopLsb(ref pw);
            row[(piece + 6) * 64 + sq] = 1;
        }
    }
    row[768] = eval;
    row[769] = 1 / (1 + MathF.Exp(-eval / 410f));

    // Convert row to CSV string
    buffer.Add(string.Join(",", row));
    generated++;

    PrintDebug(row, pos, eval);
    Array.Clear(row, 0, row.Length);

    if (buffer.Count == bufferSize)
    {
        File.AppendAllLines(output, buffer);
        Console.WriteLine($"Saved {generated} positions");
        buffer.Clear();
    }
}
// Save any remaining rows
if (buffer.Count > 0)
{
    File.AppendAllLines(output, buffer);
    Console.WriteLine($"Saved final {buffer.Count} positions, total {generated}");
}

[Conditional("DEBUG")]
static void PrintDebug(float[] row, MutablePosition pos, int eval)
{
    for (int i = 0; i < 12; i++)
    {
        int start = i * 64;
        int end = (i + 1) * 64;
        for (int j = start; j < end; j++)
        {
            if (row[j] == 1)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.Write('1');
            }
            else
                Console.Write('.');

            Console.ResetColor();
        }
        Console.WriteLine();

    }
    Console.WriteLine($"Eval: {eval}");
}