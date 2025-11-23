using Microsoft.Data.Sqlite;

namespace Chess.Api.Services;

internal class TestDatabase : IDisposable
{
    private readonly SqliteConnection _connection;
    private bool _disposed;

    public TestDatabase(string dbPath)
    {
        var connectionString = $"Data Source={dbPath}";
        _connection = new SqliteConnection(connectionString);
        _connection.Open();
    }

    public List<EnginePerformanceReport> GetTopPerformingEngines(int limit = 50)
    {
        const string query = @"
            SELECT 
                EnginePath,
                COUNT(*) AS total_positions,
                SUM(CASE WHEN IsCorrectMove = 1 THEN 1 ELSE 0 END) AS correct_positions,
                AVG(Depth) AS avg_depth,
                AVG(AvgNodes) AS avg_nodes,
                SUM(TotalNodes) AS total_nodes,
                AVG(AvgNps) AS avg_nps,
                AVG(BranchingFactor) AS avg_branching
            FROM TestResults
            GROUP BY EnginePath
            HAVING total_positions > 0
            ORDER BY 
                correct_positions DESC,
                avg_depth DESC,
                avg_nps DESC
            LIMIT @limit";

        var results = new List<EnginePerformanceReport>();
        using var command = new SqliteCommand(query, _connection);
        command.Parameters.AddWithValue("@limit", limit);
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            results.Add(new EnginePerformanceReport
            {
                EnginePath = reader.GetString(0),
                TotalPositions = Convert.ToInt32(reader.GetValue(1)),
                CorrectPositions = reader.IsDBNull(2) ? 0 : Convert.ToInt32(reader.GetValue(2)),
                AverageDepth = reader.IsDBNull(3) ? 0 : reader.GetDouble(3),
                AverageNodes = reader.IsDBNull(4) ? 0 : reader.GetDouble(4),
                TotalNodes = reader.IsDBNull(5) ? 0 : Convert.ToInt64(reader.GetValue(5)),
                AverageNps = reader.IsDBNull(6) ? 0 : reader.GetDouble(6),
                AverageBranchingFactor = reader.IsDBNull(7) ? 0 : reader.GetDouble(7)
            });
        }
        return results;
    }

    public List<PositionPerformanceReport> GetPositionPerformance(string enginePath)
    {
        const string query = @"
            SELECT 
                Category,
                FEN,
                BestMove,
                WorstMove,
                Depth,
                AvgNodes,
                TotalNodes,
                AvgNps,
                BranchingFactor,
                IsCorrectMove
            FROM TestResults
            WHERE EnginePath = @enginePath
            ORDER BY Category, Id";

        var results = new List<PositionPerformanceReport>();
        using var command = new SqliteCommand(query, _connection);
        command.Parameters.AddWithValue("@enginePath", enginePath);
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            results.Add(new PositionPerformanceReport
            {
                Category = reader.GetString(0),
                Fen = reader.GetString(1),
                BestMove = reader.GetString(2),
                WorstMove = reader.GetString(3),
                Depth = reader.GetInt32(4),
                AverageNodes = reader.GetInt32(5),
                TotalNodes = reader.GetInt64(6),
                AverageNps = reader.GetInt32(7),
                BranchingFactor = reader.GetDouble(8),
                IsCorrectMove = reader.GetInt32(9) == 1
            });
        }
        return results;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _connection.Dispose();
        _disposed = true;
    }
}

internal class EnginePerformanceReport
{
    public string EnginePath { get; set; } = string.Empty;
    public int TotalPositions { get; set; }
    public int CorrectPositions { get; set; }
    public double AverageDepth { get; set; }
    public double AverageNodes { get; set; }
    public long TotalNodes { get; set; }
    public double AverageNps { get; set; }
    public double AverageBranchingFactor { get; set; }
    public double CorrectPercentage => TotalPositions > 0 ? (double)CorrectPositions / TotalPositions * 100 : 0;
}

internal class PositionPerformanceReport
{
    public string Category { get; set; } = string.Empty;
    public string Fen { get; set; } = string.Empty;
    public string BestMove { get; set; } = string.Empty;
    public string WorstMove { get; set; } = string.Empty;
    public int Depth { get; set; }
    public int AverageNodes { get; set; }
    public long TotalNodes { get; set; }
    public int AverageNps { get; set; }
    public double BranchingFactor { get; set; }
    public bool IsCorrectMove { get; set; }
}
