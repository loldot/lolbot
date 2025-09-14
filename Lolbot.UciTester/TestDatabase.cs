using Microsoft.Data.Sqlite;

namespace Lolbot.UciTester;

public class TestDatabase : IDisposable
{
    private readonly SqliteConnection _connection;
    private bool _disposed = false;

    public TestDatabase(string dbPath)
    {
        var connectionString = $"Data Source={dbPath}";
        _connection = new SqliteConnection(connectionString);
        _connection.Open();
        InitializeDatabase();
    }

    private void InitializeDatabase()
    {
        var createTablesCommand = @"
            CREATE TABLE IF NOT EXISTS test_runs (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                commit_hash TEXT NOT NULL,
                engine_path TEXT NOT NULL,
                start_time TEXT NOT NULL,
                end_time TEXT,
                total_positions INTEGER NOT NULL DEFAULT 0,
                completed_positions INTEGER NOT NULL DEFAULT 0
            );

            CREATE TABLE IF NOT EXISTS test_results (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                test_run_id INTEGER NOT NULL,
                position_name TEXT NOT NULL,
                fen TEXT NOT NULL,
                search_depth INTEGER NOT NULL,
                actual_depth INTEGER NOT NULL,
                nodes INTEGER NOT NULL,
                calculated_nps INTEGER NOT NULL,
                time_ms INTEGER NOT NULL,
                score_cp INTEGER,
                score_mate INTEGER,
                best_move TEXT NOT NULL,
                principal_variation TEXT,
                search_time_seconds REAL NOT NULL,
                is_correct_move INTEGER NOT NULL DEFAULT 0,
                created_at TEXT NOT NULL,
                FOREIGN KEY (test_run_id) REFERENCES test_runs (id)
            );

            CREATE INDEX IF NOT EXISTS idx_test_results_run_id ON test_results(test_run_id);
            CREATE INDEX IF NOT EXISTS idx_test_results_position ON test_results(position_name);
            CREATE INDEX IF NOT EXISTS idx_test_runs_commit ON test_runs(commit_hash);
        ";

        using var command = new SqliteCommand(createTablesCommand, _connection);
        command.ExecuteNonQuery();
    }

    public long StartTestRun(string commitHash, string enginePath, int totalPositions)
    {
        var insertCommand = @"
            INSERT INTO test_runs (commit_hash, engine_path, start_time, total_positions)
            VALUES (@commitHash, @enginePath, @startTime, @totalPositions);
            SELECT last_insert_rowid();
        ";

        using var command = new SqliteCommand(insertCommand, _connection);
        command.Parameters.AddWithValue("@commitHash", commitHash);
        command.Parameters.AddWithValue("@enginePath", enginePath);
        command.Parameters.AddWithValue("@startTime", DateTime.UtcNow.ToString("O"));
        command.Parameters.AddWithValue("@totalPositions", totalPositions);

        return (long)command.ExecuteScalar()!;
    }

    public void CompleteTestRun(long testRunId, int completedPositions)
    {
        var updateCommand = @"
            UPDATE test_runs 
            SET end_time = @endTime, completed_positions = @completedPositions
            WHERE id = @testRunId
        ";

        using var command = new SqliteCommand(updateCommand, _connection);
        command.Parameters.AddWithValue("@endTime", DateTime.UtcNow.ToString("O"));
        command.Parameters.AddWithValue("@completedPositions", completedPositions);
        command.Parameters.AddWithValue("@testRunId", testRunId);
        command.ExecuteNonQuery();
    }

    public void SaveTestResult(long testRunId, string positionName, SearchResult result)
    {
        var insertCommand = @"
            INSERT INTO test_results (
                test_run_id, position_name, fen, search_depth, actual_depth, 
                nodes, calculated_nps, time_ms, score_cp, score_mate, best_move, 
                principal_variation, search_time_seconds, is_correct_move, created_at
            ) VALUES (
                @testRunId, @positionName, @fen, @searchDepth, @actualDepth,
                @nodes, @calculatedNps, @timeMs, @scoreCp, @scoreMate, @bestMove,
                @principalVariation, @searchTimeSeconds, @isCorrectMove, @createdAt
            )
        ";

        using var command = new SqliteCommand(insertCommand, _connection);
        command.Parameters.AddWithValue("@testRunId", testRunId);
        command.Parameters.AddWithValue("@positionName", positionName);
        command.Parameters.AddWithValue("@fen", result.Fen);
        command.Parameters.AddWithValue("@searchDepth", result.Depth);
        command.Parameters.AddWithValue("@actualDepth", result.ActualDepth);
        command.Parameters.AddWithValue("@nodes", result.Nodes);
        command.Parameters.AddWithValue("@calculatedNps", result.CalculatedNps);
        command.Parameters.AddWithValue("@timeMs", result.TimeMs);
        command.Parameters.AddWithValue("@scoreCp", (object?)result.ScoreCp ?? DBNull.Value);
        command.Parameters.AddWithValue("@scoreMate", (object?)result.ScoreMate ?? DBNull.Value);
        command.Parameters.AddWithValue("@bestMove", result.BestMove);
        command.Parameters.AddWithValue("@principalVariation", result.PrincipalVariation);
        command.Parameters.AddWithValue("@searchTimeSeconds", result.SearchTimeSeconds);
        command.Parameters.AddWithValue("@isCorrectMove", result.IsCorrectMove ? 1 : 0);
        command.Parameters.AddWithValue("@createdAt", DateTime.UtcNow.ToString("O"));
        command.ExecuteNonQuery();
    }

    public List<TestRunSummary> GetTestRunSummaries()
    {
        var query = @"
            SELECT 
                id, commit_hash, engine_path, start_time, end_time,
                total_positions, completed_positions
            FROM test_runs 
            ORDER BY start_time DESC
        ";

        var results = new List<TestRunSummary>();
        using var command = new SqliteCommand(query, _connection);
        using var reader = command.ExecuteReader();

        while (reader.Read())
        {
            results.Add(new TestRunSummary
            {
                Id = reader.GetInt64(0),  // id
                CommitHash = reader.GetString(1),  // commit_hash
                EnginePath = reader.GetString(2),  // engine_path
                StartTime = DateTime.Parse(reader.GetString(3)),  // start_time
                EndTime = reader.IsDBNull(4) ? null : DateTime.Parse(reader.GetString(4)),  // end_time
                TotalPositions = reader.GetInt32(5),  // total_positions
                CompletedPositions = reader.GetInt32(6)  // completed_positions
            });
        }

        return results;
    }

    public List<EnginePerformanceReport> GetTopPerformingEngines(int limit = 10)
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
                MAX(tr.start_time) as latest_test_time,
                SUM(CASE WHEN res.score_cp IS NOT NULL THEN 1 ELSE 0 END) as positions_with_score,
                AVG(CASE WHEN res.score_cp IS NOT NULL THEN ABS(res.score_cp) ELSE NULL END) as avg_abs_score
            FROM test_runs tr
            INNER JOIN test_results res ON tr.id = res.test_run_id
            WHERE tr.completed_positions = tr.total_positions
            GROUP BY tr.commit_hash
            HAVING total_positions >= 5
            ORDER BY 
                correct_positions DESC,          -- Most correct positions first
                avg_depth DESC,                  -- Then deepest search
                avg_nodes ASC,                   -- Then fewest nodes (more efficient)
                avg_calculated_nps DESC          -- Finally fastest
            LIMIT @limit
        ";

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
                LatestTestTime = DateTime.Parse(reader.GetString(7)),
                PositionsWithScore = reader.GetInt32(8),
                AverageAbsoluteScore = reader.IsDBNull(9) ? null : reader.GetDouble(9)
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
            ORDER BY res.position_name
        ";

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
                PrincipalVariation = reader.IsDBNull(9) ? "" : reader.GetString(9),
                IsCorrectMove = reader.GetInt32(10) == 1
            });
        }

        return results;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _connection?.Dispose();
            _disposed = true;
        }
    }
}

public class TestRunSummary
{
    public long Id { get; set; }
    public string CommitHash { get; set; } = "";
    public string EnginePath { get; set; } = "";
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public int TotalPositions { get; set; }
    public int CompletedPositions { get; set; }
    
    public bool IsCompleted => EndTime.HasValue;
    public double? DurationMinutes => EndTime?.Subtract(StartTime).TotalMinutes;
}

public class EnginePerformanceReport
{
    public string CommitHash { get; set; } = "";
    public int TotalPositions { get; set; }
    public int CorrectPositions { get; set; }
    public long AverageNodes { get; set; }
    public long AverageNps { get; set; }
    public double AverageTimeMs { get; set; }
    public double AverageDepth { get; set; }
    public DateTime LatestTestTime { get; set; }
    public int PositionsWithScore { get; set; }
    public double? AverageAbsoluteScore { get; set; }
    
    public double CorrectPercentage => TotalPositions > 0 ? (double)CorrectPositions / TotalPositions * 100 : 0;
}

public class PositionPerformanceReport
{
    public string PositionName { get; set; } = "";
    public string Fen { get; set; } = "";
    public string BestMove { get; set; } = "";
    public int ActualDepth { get; set; }
    public long Nodes { get; set; }
    public long Nps { get; set; }
    public int TimeMs { get; set; }
    public int? ScoreCp { get; set; }
    public int? ScoreMate { get; set; }
    public string PrincipalVariation { get; set; } = "";
    public bool IsCorrectMove { get; set; }
}