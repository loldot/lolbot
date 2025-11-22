using Microsoft.Data.Sqlite;

namespace Lolbot.Benchmarking;

public static class Database
{
    public static async Task Init(this SqliteConnection db)
    {
        var command = db.CreateCommand();
        command.CommandText = @"
            CREATE TABLE IF NOT EXISTS TestResults (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                EnginePath TEXT NOT NULL,
                Category TEXT NOT NULL,
                FEN TEXT NOT NULL,
                BestMove TEXT NOT NULL,
                WorstMove TEXT NOT NULL,
                Depth INTEGER NOT NULL,
                AvgNodes INTEGER NOT NULL,
                TotalNodes INTEGER NOT NULL,
                AvgNps INTEGER NOT NULL,
                BranchingFactor REAL NOT NULL,
                IsCorrectMove BOOLEAN NOT NULL
            );
        ";
        await command.ExecuteNonQueryAsync();
    }

    public static async Task Insert(
        this SqliteConnection db, 
        string enginePath, 
        (string Category, string FEN, string BestMove, string WorstMove) position, 
        (int Depth, int AvgNodes, int TotalNodes, int AvgNps, double BranchingFactor) result, 
        bool isCorrectMove
    )
    {
        var command = db.CreateCommand();
        command.CommandText = @"
            INSERT INTO TestResults (EnginePath, Category, FEN, BestMove, WorstMove, Depth, AvgNodes, TotalNodes, AvgNps, BranchingFactor, IsCorrectMove)
            VALUES ($enginePath, $category, $fen, $bestMove, $worstMove, $depth, $avgNodes, $totalNodes, $avgNps, $branchingFactor, $isCorrectMove);
        ";
        command.Parameters.AddWithValue("$enginePath", enginePath);
        command.Parameters.AddWithValue("$category", position.Category);
        command.Parameters.AddWithValue("$fen", position.FEN);
        command.Parameters.AddWithValue("$bestMove", position.BestMove);
        command.Parameters.AddWithValue("$worstMove", position.WorstMove);
        command.Parameters.AddWithValue("$depth", result.Depth);
        command.Parameters.AddWithValue("$avgNodes", result.AvgNodes);
        command.Parameters.AddWithValue("$totalNodes", result.TotalNodes);
        command.Parameters.AddWithValue("$avgNps", result.AvgNps);
        command.Parameters.AddWithValue("$branchingFactor", result.BranchingFactor);
        command.Parameters.AddWithValue("$isCorrectMove", isCorrectMove);

        await command.ExecuteNonQueryAsync();
    }
}
