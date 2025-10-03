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
        var query = @"
            SELECT 
                tr.commit_hash,
                COUNT(res.id) as total_positions,
                SUM(res.is_correct_move) as correct_positions,
                AVG(res.nodes) as avg_nodes,
                AVG(res.calculated_nps) as avg_calculated_nps,
                AVG(res.time_ms) as avg_time_ms,
                AVG(res.actual_depth) as avg_depth,
                MAX(tr.start_time) as latest_test_time
            FROM test_runs tr
            INNER JOIN test_results res ON tr.id = res.test_run_id
            WHERE tr.completed_positions = tr.total_positions
            GROUP BY tr.commit_hash
            HAVING total_positions >= 1
            ORDER BY 
                correct_positions DESC,
                avg_depth DESC,
                avg_nodes ASC,
                avg_calculated_nps DESC
            LIMIT @limit";

        var results = new List<EnginePerformanceReport>();
        using var command = new SqliteCommand(query, _connection);
        command.Parameters.AddWithValue("@limit", limit);
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            results.Add(new EnginePerformanceReport
            {
                CommitHash = reader.GetString(0),
                TotalPositions = reader.GetInt32(1),
                CorrectPositions = reader.GetInt32(2),
                AverageNodes = (long)reader.GetDouble(3),
                AverageNps = (long)reader.GetDouble(4),
                AverageTimeMs = reader.GetDouble(5),
                AverageDepth = reader.GetDouble(6),
                LatestTestTime = DateTime.Parse(reader.GetString(7))
            });
        }
        return results;
    }

    public List<PositionPerformanceReport> GetPositionPerformance(string commitHash)
    {
        var query = @"
            SELECT 
                res.position_name,
                res.fen,
                res.best_move,
                res.actual_depth,
                res.nodes,
                res.calculated_nps,
                res.time_ms,
                res.score_cp,
                res.score_mate,
                res.principal_variation,
                res.is_correct_move
            FROM test_runs tr
            INNER JOIN test_results res ON tr.id = res.test_run_id
            WHERE tr.commit_hash = @commitHash
            ORDER BY res.position_name";

        var results = new List<PositionPerformanceReport>();
        using var command = new SqliteCommand(query, _connection);
        command.Parameters.AddWithValue("@commitHash", commitHash);
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            results.Add(new PositionPerformanceReport
            {
                PositionName = reader.GetString(0),
                Fen = reader.GetString(1),
                BestMove = reader.GetString(2),
                ActualDepth = reader.GetInt32(3),
                Nodes = reader.GetInt64(4),
                Nps = reader.GetInt64(5),
                TimeMs = reader.GetInt32(6),
                ScoreCp = reader.IsDBNull(7) ? null : reader.GetInt32(7),
                ScoreMate = reader.IsDBNull(8) ? null : reader.GetInt32(8),
                PrincipalVariation = reader.IsDBNull(9) ? string.Empty : reader.GetString(9),
                IsCorrectMove = reader.GetInt32(10) == 1
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
    public string CommitHash { get; set; } = string.Empty;
    public int TotalPositions { get; set; }
    public int CorrectPositions { get; set; }
    public long AverageNodes { get; set; }
    public long AverageNps { get; set; }
    public double AverageTimeMs { get; set; }
    public double AverageDepth { get; set; }
    public DateTime LatestTestTime { get; set; }
    public double CorrectPercentage => TotalPositions > 0 ? (double)CorrectPositions / TotalPositions * 100 : 0;
}

internal class PositionPerformanceReport
{
    public string PositionName { get; set; } = string.Empty;
    public string Fen { get; set; } = string.Empty;
    public string BestMove { get; set; } = string.Empty;
    public int ActualDepth { get; set; }
    public long Nodes { get; set; }
    public long Nps { get; set; }
    public int TimeMs { get; set; }
    public int? ScoreCp { get; set; }
    public int? ScoreMate { get; set; }
    public string PrincipalVariation { get; set; } = string.Empty;
    public bool IsCorrectMove { get; set; }
}
