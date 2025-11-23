using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
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

    public List<EnginePerformanceReport> GetTopPerformingEngines(int limit = 250)
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

        var commitLookup = LoadCommitLookup();
        var commitRecords = commitLookup.Values.ToList();
        foreach (var report in results)
        {
            var metadata = ResolveCommitMetadata(commitLookup, commitRecords, report.EnginePath);
            report.EngineFolder = metadata.EngineFolder;
            report.CommitHash = metadata.CommitHash;
            report.CommittedAt = metadata.CommittedAt;
        }

        return results
            .OrderBy(r => r.CommittedAt ?? DateTimeOffset.MinValue)
            .ThenBy(r => r.EngineFolder, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public List<PositionPerformanceReport> GetPositionPerformance(string engineIdentifier)
    {
        var enginePath = ResolveEnginePath(engineIdentifier);
        if (string.IsNullOrWhiteSpace(enginePath)) return new List<PositionPerformanceReport>();

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

    public List<AvailablePosition> GetAvailablePositions()
    {
        const string query = @"
            SELECT 
                Category,
                FEN,
                BestMove,
                WorstMove,
                COUNT(*) AS attempts
            FROM TestResults
            GROUP BY Category, FEN, BestMove, WorstMove
            ORDER BY Category, FEN";

        var results = new List<AvailablePosition>();
        using var command = new SqliteCommand(query, _connection);
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            results.Add(new AvailablePosition
            {
                Category = reader.GetString(0),
                Fen = reader.GetString(1),
                BestMove = reader.GetString(2),
                WorstMove = reader.GetString(3),
                Attempts = reader.IsDBNull(4) ? 0 : Convert.ToInt32(reader.GetValue(4))
            });
        }

        return results;
    }

    public List<PositionHistoryPoint> GetPositionHistory(string fen, int limit = 500)
    {
        if (string.IsNullOrWhiteSpace(fen)) return new List<PositionHistoryPoint>();

        const string query = @"
            SELECT 
                EnginePath,
                Category,
                BestMove,
                WorstMove,
                Depth,
                AvgNodes,
                TotalNodes,
                AvgNps,
                BranchingFactor,
                IsCorrectMove
            FROM TestResults
            WHERE FEN = @fen
            ORDER BY Id DESC
            LIMIT @limit";

        var results = new List<PositionHistoryPoint>();
        var commitLookup = LoadCommitLookup();
        var commitRecords = commitLookup.Values.ToList();

        using var command = new SqliteCommand(query, _connection);
        command.Parameters.AddWithValue("@fen", fen);
        command.Parameters.AddWithValue("@limit", limit);
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var enginePath = reader.GetString(0);
            var metadata = ResolveCommitMetadata(commitLookup, commitRecords, enginePath);

            results.Add(new PositionHistoryPoint
            {
                EnginePath = enginePath,
                EngineFolder = metadata.EngineFolder,
                CommitHash = metadata.CommitHash,
                CommittedAt = metadata.CommittedAt,
                Category = reader.GetString(1),
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

        return results
            .OrderBy(p => p.CommittedAt ?? DateTimeOffset.MinValue)
            .ThenBy(p => p.EngineFolder, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private string? ResolveEnginePath(string engineIdentifier)
    {
        if (string.IsNullOrWhiteSpace(engineIdentifier)) return null;
        if (LooksLikePath(engineIdentifier)) return engineIdentifier;

        const string query = "SELECT EnginePath FROM TestResults WHERE EnginePath LIKE @pattern COLLATE NOCASE ORDER BY Id DESC LIMIT 1";
        using var command = new SqliteCommand(query, _connection);
        command.Parameters.AddWithValue("@pattern", $"%{engineIdentifier}%");
        var result = command.ExecuteScalar();
        return result is string path ? path : null;
    }

    private static bool LooksLikePath(string value)
    {
        return value.Contains(':') || value.Contains('/') || value.Contains('\\');
    }

    private CommitMetadata ResolveCommitMetadata(Dictionary<string, CommitRecord> lookup, List<CommitRecord> records, string enginePath)
    {
        var folder = ExtractEngineFolder(enginePath) ?? string.Empty;

        if (!string.IsNullOrEmpty(folder) && lookup.TryGetValue(folder, out var direct))
        {
            return new CommitMetadata(folder, direct.Hash, direct.CommittedAt);
        }

        foreach (var record in records)
        {
            if (!string.IsNullOrEmpty(folder) && folder.IndexOf(record.Hash, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return new CommitMetadata(folder, record.Hash, record.CommittedAt);
            }

            if (enginePath.IndexOf(record.Hash, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return new CommitMetadata(folder, record.Hash, record.CommittedAt);
            }
        }

        var fallbackHash = string.IsNullOrEmpty(folder) ? enginePath : folder;
        return new CommitMetadata(folder, fallbackHash, null);
    }

    private Dictionary<string, CommitRecord> LoadCommitLookup()
    {
        const string query = "SELECT hash, committed_at FROM commits";
        var lookup = new Dictionary<string, CommitRecord>(StringComparer.OrdinalIgnoreCase);
        try
        {
            using var command = new SqliteCommand(query, _connection);
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                if (reader.IsDBNull(0)) continue;
                var hash = reader.GetString(0);
                if (string.IsNullOrWhiteSpace(hash)) continue;

                var committedAt = ConvertToDateTimeOffset(reader.GetValue(1));
                lookup[hash] = new CommitRecord(hash, committedAt);
            }
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == 1)
        {
            // commits table has not been created yet; ignore and proceed without commit metadata.
            return lookup;
        }

        return lookup;
    }

    private static DateTimeOffset? ConvertToDateTimeOffset(object? value)
    {
        if (value is null || value == DBNull.Value) return null;
        if (value is DateTimeOffset dto) return dto;
        if (value is DateTime dt) return new DateTimeOffset(dt);

        var str = Convert.ToString(value, CultureInfo.InvariantCulture);
        if (string.IsNullOrWhiteSpace(str)) return null;
        if (DateTimeOffset.TryParse(str, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed))
        {
            return parsed;
        }

        return null;
    }

    private static string? ExtractEngineFolder(string enginePath)
    {
        if (string.IsNullOrWhiteSpace(enginePath)) return null;
        var trimmed = enginePath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (string.IsNullOrEmpty(trimmed)) return null;

        var directory = Path.GetDirectoryName(trimmed);
        if (string.IsNullOrEmpty(directory))
        {
            var fileName = Path.GetFileName(trimmed);
            return string.IsNullOrWhiteSpace(fileName) ? null : fileName;
        }

        var folder = Path.GetFileName(directory);
        return string.IsNullOrWhiteSpace(folder) ? Path.GetFileName(trimmed) : folder;
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
    public string EngineFolder { get; set; } = string.Empty;
    public string CommitHash { get; set; } = string.Empty;
    public DateTimeOffset? CommittedAt { get; set; }
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

internal class AvailablePosition
{
    public string Category { get; set; } = string.Empty;
    public string Fen { get; set; } = string.Empty;
    public string BestMove { get; set; } = string.Empty;
    public string WorstMove { get; set; } = string.Empty;
    public int Attempts { get; set; }
}

internal class PositionHistoryPoint
{
    public string EnginePath { get; set; } = string.Empty;
    public string EngineFolder { get; set; } = string.Empty;
    public string CommitHash { get; set; } = string.Empty;
    public DateTimeOffset? CommittedAt { get; set; }
    public string Category { get; set; } = string.Empty;
    public string BestMove { get; set; } = string.Empty;
    public string WorstMove { get; set; } = string.Empty;
    public int Depth { get; set; }
    public int AverageNodes { get; set; }
    public long TotalNodes { get; set; }
    public int AverageNps { get; set; }
    public double BranchingFactor { get; set; }
    public bool IsCorrectMove { get; set; }
}

internal sealed record CommitMetadata(string EngineFolder, string CommitHash, DateTimeOffset? CommittedAt);
internal sealed record CommitRecord(string Hash, DateTimeOffset? CommittedAt);
