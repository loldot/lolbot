using System.CommandLine;
using Lolbot.UciTester;

// Test command
var commitHashArgument = new Argument<string>(
    name: "commit",
    description: "The short commit hash to test");

var depthOption = new Option<int>(
    name: "--depth",
    description: "Search depth for each position",
    getDefaultValue: () => 14);

var dbPathOption = new Option<string>(
    name: "--db",
    description: "Path to SQLite database file",
    getDefaultValue: () => "test_results.db");

var engineDirOption = new Option<string>(
    name: "--engine-dir",
    description: "Base directory for engine versions",
    getDefaultValue: () => @"C:\dev\lolbot-versions");

var categoriesOption = new Option<string[]>(
    name: "--categories",
    description: "Test categories to run (CCC, CCC_Avoid)",
    getDefaultValue: () => new[] { "CCC" });

var logOption = new Option<bool>(
    name: "--log",
    description: "Enable UCI communication logging to console",
    getDefaultValue: () => false);

var testCommand = new Command("test", "Run UCI tests against an engine")
{
    commitHashArgument,
    depthOption,
    dbPathOption,
    engineDirOption,
    categoriesOption,
    logOption
};

// Report command
var reportDbOption = new Option<string>(
    name: "--db",
    description: "Path to SQLite database file",
    getDefaultValue: () => "test_results.db");

var limitOption = new Option<int>(
    name: "--limit",
    description: "Number of top engines to show",
    getDefaultValue: () => 10);

var detailCommitOption = new Option<string?>(
    name: "--detail",
    description: "Show detailed results for a specific commit",
    getDefaultValue: () => null);

var reportCommand = new Command("report", "Generate performance reports")
{
    reportDbOption,
    limitOption,
    detailCommitOption
};

var rootCommand = new RootCommand("UCI Engine Tester")
{
    testCommand,
    reportCommand
};

testCommand.SetHandler((string commit, int depth, string dbPath, string engineDir, string[] categories, bool enableLog) =>
{
    try
    {
        RunTests(commit, depth, dbPath, engineDir, categories, enableLog);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error: {ex.Message}");
        Environment.Exit(1);
    }
}, commitHashArgument, depthOption, dbPathOption, engineDirOption, categoriesOption, logOption);

reportCommand.SetHandler((string dbPath, int limit, string? detailCommit) =>
{
    try
    {
        GenerateReport(dbPath, limit, detailCommit);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error: {ex.Message}");
        Environment.Exit(1);
    }
}, reportDbOption, limitOption, detailCommitOption);

return rootCommand.Invoke(args);

static void RunTests(string commitHash, int depth, string dbPath, string engineDir, string[] categories, bool enableLogging)
{
    var enginePath = Path.Combine(engineDir, commitHash, "Lolbot.Engine.exe");
    
    if (!File.Exists(enginePath))
    {
        throw new FileNotFoundException($"Engine not found at: {enginePath}");
    }

    Console.WriteLine($"Testing engine: {enginePath}");
    Console.WriteLine($"Commit: {commitHash}");
    Console.WriteLine($"Search depth: {depth}");
    Console.WriteLine($"Database: {dbPath}");
    if (enableLogging)
    {
        Console.WriteLine("UCI communication logging: Enabled");
    }
    Console.WriteLine($"Categories: {string.Join(", ", categories)}");
    Console.WriteLine();

    // Get positions to test
    var allPositions = TestPositions.GetAllPositions();
    var positionsToTest = allPositions
        .Where(p => categories.Contains(p.Category))
        .ToList();

    Console.WriteLine($"Found {positionsToTest.Count} positions to test");
    Console.WriteLine();

    // Initialize database
    using var database = new TestDatabase(dbPath);
    var testRunId = database.StartTestRun(commitHash, enginePath, positionsToTest.Count);

    Console.WriteLine($"Started test run {testRunId}");
    
    var completedPositions = 0;
    var correctPositions = 0;
    var startTime = DateTime.UtcNow;

    using var engine = new UciEngine(enginePath, enableLogging);
    engine.Initialize();
    Console.WriteLine("Engine initialized");
    Console.WriteLine();

    foreach (var position in positionsToTest)
    {
        try
        {
            Console.Write($"Testing {position.Name}... ");
            
            var result = engine.SearchPosition(position.Fen, depth);
            
            // Check if the engine found the correct move
            result.IsCorrectMove = !string.IsNullOrEmpty(position.ExpectedBestMoveUci) &&
                                  string.Equals(result.BestMove, position.ExpectedBestMoveUci, StringComparison.OrdinalIgnoreCase);
            
            database.SaveTestResult(testRunId, position.Name, result);
            
            completedPositions++;
            if (result.IsCorrectMove) correctPositions++;
            
            var correctIndicator = result.IsCorrectMove ? "✓" : "✗";
            Console.WriteLine($"Done {correctIndicator}");
            
            if (!string.IsNullOrEmpty(position.ExpectedBestMoveUci))
            {
                Console.WriteLine($"  Best move: {result.BestMove} {(result.IsCorrectMove ? "(correct)" : "(expected: " + position.ExpectedBestMove + " = " + position.ExpectedBestMoveUci + ")")}");
            }
            else
            {
                Console.WriteLine($"  Best move: {result.BestMove}");
            }
            
            Console.WriteLine($"  Depth: {result.ActualDepth}");
            Console.WriteLine($"  Nodes: {result.Nodes:N0}");
            Console.WriteLine($"  NPS: {result.CalculatedNps:N0}");
            Console.WriteLine($"  Time: {result.SearchTimeSeconds:F2}s");
            
            if (result.ScoreCp.HasValue)
                Console.WriteLine($"  Score: {result.ScoreCp.Value}cp");
            else if (result.ScoreMate.HasValue)
                Console.WriteLine($"  Score: Mate in {result.ScoreMate.Value}");
            
            if (!string.IsNullOrEmpty(result.PrincipalVariation))
                Console.WriteLine($"  PV: {result.PrincipalVariation}");
                
            Console.WriteLine();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed: {ex.Message}");
            Console.WriteLine();
        }
    }

    database.CompleteTestRun(testRunId, completedPositions);
    
    var totalTime = DateTime.UtcNow - startTime;
    Console.WriteLine($"Test run completed!");
    Console.WriteLine($"Completed {completedPositions}/{positionsToTest.Count} positions");
    Console.WriteLine($"Correct positions: {correctPositions}/{completedPositions} ({(double)correctPositions / completedPositions * 100:F1}%)");
    Console.WriteLine($"Total time: {totalTime.TotalMinutes:F1} minutes");
    Console.WriteLine($"Average time per position: {totalTime.TotalSeconds / completedPositions:F1} seconds");
}

static void GenerateReport(string dbPath, int limit, string? detailCommit)
{
    if (!File.Exists(dbPath))
    {
        Console.WriteLine($"Database file not found: {dbPath}");
        Environment.Exit(1);
    }

    using var database = new TestDatabase(dbPath);
    
    if (!string.IsNullOrEmpty(detailCommit))
    {
        GenerateDetailedReport(database, detailCommit);
    }
    else
    {
        GenerateTopEnginesReport(database, limit);
    }
}

static void GenerateTopEnginesReport(TestDatabase database, int limit)
{
    Console.WriteLine($"=== TOP {limit} PERFORMING ENGINE VERSIONS ===");
    Console.WriteLine();
    
    var topEngines = database.GetTopPerformingEngines(limit);
    
    if (!topEngines.Any())
    {
        Console.WriteLine("No completed test runs found in database.");
        return;
    }
    
    Console.WriteLine($"{"Rank",-4} {"Commit",-8} {"Correct",-10} {"Positions",-9} {"Avg NPS",-12} {"Avg Nodes",-12} {"Avg Time",-10} {"Avg Depth",-10} {"Latest Test",-12}");
    Console.WriteLine(new string('-', 95));
    
    for (int i = 0; i < topEngines.Count; i++)
    {
        var engine = topEngines[i];
        var correctDisplay = $"{engine.CorrectPositions}/{engine.TotalPositions}";
        Console.WriteLine($"{i + 1,-4} {engine.CommitHash,-8} {correctDisplay,-10} {engine.TotalPositions,-9} {engine.AverageNps,-12:N0} {engine.AverageNodes,-12:N0} {engine.AverageTimeMs,-10:F0}ms {engine.AverageDepth,-10:F1} {engine.LatestTestTime,-12:MM/dd HH:mm}");
    }
    
    Console.WriteLine();
    Console.WriteLine("Performance Insights:");
    
    var bestCorrect = topEngines.OrderByDescending(e => e.CorrectPositions).ThenByDescending(e => e.CorrectPercentage).First();
    Console.WriteLine($"• Most correct: {bestCorrect.CommitHash} with {bestCorrect.CorrectPositions}/{bestCorrect.TotalPositions} ({bestCorrect.CorrectPercentage:F1}%)");
    
    var bestNps = topEngines.OrderByDescending(e => e.AverageNps).First();
    Console.WriteLine($"• Fastest: {bestNps.CommitHash} with {bestNps.AverageNps:N0} NPS");
    
    var bestDepth = topEngines.OrderByDescending(e => e.AverageDepth).First();
    Console.WriteLine($"• Deepest: {bestDepth.CommitHash} with {bestDepth.AverageDepth:F1} average depth");
    
    var mostEfficient = topEngines.OrderBy(e => e.AverageNodes).First();
    Console.WriteLine($"• Most efficient: {mostEfficient.CommitHash} with {mostEfficient.AverageNodes:N0} average nodes");
    
    var mostTested = topEngines.OrderByDescending(e => e.TotalPositions).First();
    Console.WriteLine($"• Most tested: {mostTested.CommitHash} with {mostTested.TotalPositions} positions");
    
    Console.WriteLine();
    Console.WriteLine("Use --detail <commit> to see detailed results for a specific engine version.");
}

static void GenerateDetailedReport(TestDatabase database, string commitHash)
{
    Console.WriteLine($"=== DETAILED REPORT FOR ENGINE {commitHash.ToUpper()} ===");
    Console.WriteLine();
    
    var positions = database.GetPositionPerformance(commitHash);
    
    if (!positions.Any())
    {
        Console.WriteLine($"No test results found for commit: {commitHash}");
        return;
    }
    
    Console.WriteLine($"{"Position",-15} {"Best Move",-10} {"Correct",-7} {"Depth",-6} {"Nodes",-10} {"NPS",-10} {"Time",-8} {"Score",-12}");
    Console.WriteLine(new string('-', 82));
    
    foreach (var pos in positions)
    {
        var scoreStr = pos.ScoreCp?.ToString() + "cp" ?? 
                      (pos.ScoreMate?.ToString() + "M" ?? "N/A");
        var correctStr = pos.IsCorrectMove ? "✓" : "✗";
        
        Console.WriteLine($"{pos.PositionName,-15} {pos.BestMove,-10} {correctStr,-7} {pos.ActualDepth,-6} {pos.Nodes,-10:N0} {pos.Nps,-10:N0} {pos.TimeMs,-8}ms {scoreStr,-12}");
    }
    
    Console.WriteLine();
    Console.WriteLine("Summary Statistics:");
    var correctCount = positions.Count(p => p.IsCorrectMove);
    Console.WriteLine($"• Total positions tested: {positions.Count}");
    Console.WriteLine($"• Correct positions: {correctCount}/{positions.Count} ({(double)correctCount / positions.Count * 100:F1}%)");
    Console.WriteLine($"• Average nodes: {positions.Average(p => p.Nodes):N0}");
    Console.WriteLine($"• Average NPS: {positions.Average(p => p.Nps):N0}");
    Console.WriteLine($"• Average depth: {positions.Average(p => p.ActualDepth):F1}");
    Console.WriteLine($"• Average time: {positions.Average(p => p.TimeMs):F0}ms");
    
    var withScores = positions.Where(p => p.ScoreCp.HasValue).ToList();
    if (withScores.Any())
    {
        Console.WriteLine($"• Average evaluation: {withScores.Average(p => p.ScoreCp!.Value):F0}cp");
    }
}