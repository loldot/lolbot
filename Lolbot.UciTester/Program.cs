using Lolbot.Core;
using Microsoft.Data.Sqlite;
using Lolbot.Benchmarking;

// select EnginePath, avg(Depth) as D, sum(TotalNodes) as TN, 
//    avg(avgNodes) as AN, avg(AvgNps) as nps, avg(BranchingFactor) as bf, 
//    sum(IsCorrectMove) as Score 
// from TestResults 
// group by EnginePath 
// order by 7 desc;

string positionsFile = "positions.csv";
string dbPath = @"C:\dev\chess-data\test_results.db";

string engineFolder = @"C:\dev\lolbot-versions";
string exeName = "Lolbot.Engine.exe";

using var db = new SqliteConnection($"Data Source={dbPath}");
db.Open();
await db.Init();

string[] enginesToTest;
if (args.Length > 0 && args[0].Trim().Equals("current"))
{
    enginesToTest = [@"C:\dev\lolbot\Lolbot.Engine\bin\Release\net10.0\win-x64\publish\"];
}
else
{
    enginesToTest = [.. Directory.EnumerateDirectories(engineFolder)];
}

var positions = new List<(string Category, string FEN, string BestMove, string WorstMove)>();
var lines = File.ReadAllLines(positionsFile);
foreach (var line in lines.Skip(1))
{
    var parts = line.Split(',');
    if (parts.Length >= 4)
    {
        positions.Add((parts[0], parts[1], parts[2], parts[3]));
    }
}

foreach (var engine in enginesToTest)
{
    string path = Path.Combine(engine, exeName);
    if (!File.Exists(path))
    {
        Console.WriteLine($"Engine not found: {path}");
        continue;
    }
    try
    {
        await TestEngine(path);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error testing engine {path}: {ex.Message}");
        continue;
    }
}

Environment.Exit(0);

async Task TestEngine(string enginePath)
{

    int bestMovesFound = 0;
    int worstMovesAvoided = 0;

    using UciDriver uci = new UciDriver(enginePath);
    uci.Uci();

    foreach (var pos in positions)
    {
        Console.WriteLine($"{pos.Category} {pos.FEN}");
        string expected = pos.BestMove;
        Mode mode = Mode.BestMove;

        if (string.IsNullOrEmpty(pos.BestMove))
        {
            mode = Mode.AvoidMove;
            expected = pos.WorstMove;
        }

        uci.IsReady();
        uci.SetPosition(pos.FEN);
        uci.Go(new()
        {
            WhiteTime = 50_000,
            BlackTime = 50_000,
            WhiteInc = 100,
            BlackInc = 100,
        });
        int timeout = 0;
        while (!uci.IsFinished && timeout < 500)
        {
            await Task.Delay(100);
            timeout++;
        }
        if (timeout >= 500)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("Engine timed out!");
            Console.ResetColor();
            uci.Dispose();

            return;
        }

        var (totalDepth, averageNodes, totalNodes, averageNps, branchingFactor) = uci.SearchStats;

        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"[Engine Stats] Average Nodes: {averageNodes:N0}");
        Console.WriteLine($"[Engine Stats] Branching Factor: {branchingFactor:N3}");
        Console.WriteLine($"[Engine Stats] Average NPS: {averageNps:N0}");
        Console.WriteLine($"[Engine Stats] Total Depth: {totalDepth:N0}");
        Console.ResetColor();

        Console.ForegroundColor = ConsoleColor.Green;
        bool success = false;
        if (mode == Mode.BestMove && uci.BestMove == expected)
        {
            Console.WriteLine("Best move found!");
            bestMovesFound++;
            success = true;
        }
        else if (mode == Mode.AvoidMove && uci.BestMove != expected)
        {
            Console.WriteLine("Worst move avoided!");
            worstMovesAvoided++;
            success = true;
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Failed! Expected: {expected}, Got: {uci.BestMove}");
        }

        Console.ResetColor();
        uci.ClearMove();

        await db.Insert(enginePath, pos, uci.SearchStats, success);
    }

    int successCount = bestMovesFound + worstMovesAvoided;
    Console.WriteLine($"Solved {successCount} / {positions.Count} positions");
}


public enum Mode : byte
{
    BestMove = 0,
    AvoidMove = 1
}