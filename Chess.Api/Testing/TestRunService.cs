using Microsoft.Data.Sqlite;

namespace Chess.Api.Testing;

public class TestRunService
{
    private readonly string _dbPath;
    private readonly ILogger<TestRunService> _logger;
    private readonly object _lock = new();
    private bool _running;

    public TestRunService(string dbPath, ILogger<TestRunService> logger)
    {
        _dbPath = dbPath;
        _logger = logger;
        EnsureSchema();
    }

    private SqliteConnection Open() { var c = new SqliteConnection($"Data Source={_dbPath}"); c.Open(); return c; }

    private void EnsureSchema()
    {
        using var c = Open();
        var sql = @"CREATE TABLE IF NOT EXISTS test_runs (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                commit_hash TEXT NOT NULL,
                engine_path TEXT NOT NULL,
                start_time TEXT NOT NULL,
                end_time TEXT,
                total_positions INTEGER NOT NULL DEFAULT 0,
                completed_positions INTEGER NOT NULL DEFAULT 0);
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
                FOREIGN KEY (test_run_id) REFERENCES test_runs (id));";
        new SqliteCommand(sql, c).ExecuteNonQuery();
    }

    public async Task<TestRunResponse> StartRunAsync(string commitHash, string enginePath, int depth, string[] categories, CancellationToken ct = default)
    {
        if (!File.Exists(enginePath)) throw new FileNotFoundException(enginePath);
        lock (_lock)
        {
            if (_running) throw new InvalidOperationException("A test run is already in progress");
            _running = true;
        }
        try
        {
            var positions = TestPositions.GetAllPositions().Where(p => categories.Contains(p.Category)).ToList();
            long runId;
            using (var c = Open())
            {
                var cmd = c.CreateCommand();
                cmd.CommandText = "INSERT INTO test_runs (commit_hash, engine_path, start_time, total_positions) VALUES (@c,@e,@s,@t); SELECT last_insert_rowid();";
                cmd.Parameters.AddWithValue("@c", commitHash);
                cmd.Parameters.AddWithValue("@e", enginePath);
                cmd.Parameters.AddWithValue("@s", DateTime.UtcNow.ToString("O"));
                cmd.Parameters.AddWithValue("@t", positions.Count);
                runId = (long)(cmd.ExecuteScalar()!);
            }
            _ = Task.Run(() => ExecuteAsync(runId, commitHash, enginePath, depth, positions, ct));
            return new TestRunResponse(runId, commitHash, positions.Count, 0, false);
        }
        catch { _running = false; throw; }
    }

    private void ExecuteAsync(long runId, string commitHash, string enginePath, int depth, List<TestPosition> positions, CancellationToken ct)
    {
        try
        {
            using var engine = new UciEngine(enginePath);
            engine.Initialize();
            var completed = 0;
            foreach (var pos in positions)
            {
                if (ct.IsCancellationRequested) break;
                var result = engine.SearchPosition(pos.Fen, depth);
                result.IsCorrectMove = pos.ExpectedBestMoveUci != null && string.Equals(result.BestMove, pos.ExpectedBestMoveUci, StringComparison.OrdinalIgnoreCase);
                SaveResult(runId, pos.Name, result);
                completed++;
            }
            CompleteRun(runId, completed);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Test run {RunId} failed", runId);
        }
        finally { _running = false; }
    }

    private void SaveResult(long runId, string positionName, SearchResult r)
    {
        using var c = Open();
        var sql = @"INSERT INTO test_results (test_run_id, position_name, fen, search_depth, actual_depth, nodes, calculated_nps, time_ms, score_cp, score_mate, best_move, principal_variation, search_time_seconds, is_correct_move, created_at) 
                    VALUES (@id,@p,@f,@sd,@ad,@n,@nps,@tm,@cp,@mate,@bm,@pv,@sts,@ic,@ca);";
        var cmd = c.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@id", runId);
        cmd.Parameters.AddWithValue("@p", positionName);
        cmd.Parameters.AddWithValue("@f", r.Fen);
        cmd.Parameters.AddWithValue("@sd", r.Depth);
        cmd.Parameters.AddWithValue("@ad", r.ActualDepth);
        cmd.Parameters.AddWithValue("@n", r.Nodes);
        cmd.Parameters.AddWithValue("@nps", r.CalculatedNps);
        cmd.Parameters.AddWithValue("@tm", r.TimeMs);
        cmd.Parameters.AddWithValue("@cp", (object?)r.ScoreCp ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@mate", (object?)r.ScoreMate ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@bm", r.BestMove);
        cmd.Parameters.AddWithValue("@pv", r.PrincipalVariation);
        cmd.Parameters.AddWithValue("@sts", r.SearchTimeSeconds);
        cmd.Parameters.AddWithValue("@ic", r.IsCorrectMove ? 1 : 0);
        cmd.Parameters.AddWithValue("@ca", DateTime.UtcNow.ToString("O"));
        cmd.ExecuteNonQuery();
    }

    private void CompleteRun(long runId, int completed)
    {
        using var c = Open();
        var cmd = c.CreateCommand();
        cmd.CommandText = "UPDATE test_runs SET end_time=@e, completed_positions=@cp WHERE id=@id";
        cmd.Parameters.AddWithValue("@e", DateTime.UtcNow.ToString("O"));
        cmd.Parameters.AddWithValue("@cp", completed);
        cmd.Parameters.AddWithValue("@id", runId);
        cmd.ExecuteNonQuery();
    }

    public IEnumerable<TestRunResponse> GetRuns()
    {
        using var c = Open();
        var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT id, commit_hash, total_positions, completed_positions, end_time IS NOT NULL FROM test_runs ORDER BY id DESC";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            yield return new TestRunResponse(reader.GetInt64(0), reader.GetString(1), reader.GetInt32(2), reader.GetInt32(3), reader.GetBoolean(4));
        }
    }
}

public record TestRunResponse(long Id, string CommitHash, int TotalPositions, int CompletedPositions, bool Completed);