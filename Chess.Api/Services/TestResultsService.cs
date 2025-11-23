namespace Chess.Api.Services;

public class TestResultsService
{
    private readonly string _dbPath;
    public TestResultsService(string dbPath)
    {
        _dbPath = dbPath;
    }

    public IEnumerable<EngineSummaryDto> GetEngineSummaries(int limit = 250)
    {
        if (!File.Exists(_dbPath)) return Enumerable.Empty<EngineSummaryDto>();

        var effectiveLimit = Math.Clamp(limit, 1, 1000);

        using var db = new TestDatabase(_dbPath);
        var reports = db.GetTopPerformingEngines(effectiveLimit);
        return reports.Select(r => new EngineSummaryDto
        {
            EnginePath = r.EnginePath,
            EngineFolder = r.EngineFolder,
            CommitHash = r.CommitHash,
            CommittedAt = r.CommittedAt,
            TotalPositions = r.TotalPositions,
            CorrectPositions = r.CorrectPositions,
            CorrectPercentage = r.CorrectPercentage,
            AverageDepth = r.AverageDepth,
            AverageNodes = r.AverageNodes,
            TotalNodes = r.TotalNodes,
            AverageNps = r.AverageNps,
            AverageBranchingFactor = r.AverageBranchingFactor
        }).ToList();
    }

    public IEnumerable<PositionResultDto> GetPositionResults(string engineIdentifier)
    {
        if (!File.Exists(_dbPath)) return Enumerable.Empty<PositionResultDto>();

        using var db = new TestDatabase(_dbPath);
        var positions = db.GetPositionPerformance(engineIdentifier);
        if (positions.Count == 0) return Enumerable.Empty<PositionResultDto>();
        return positions.Select(p => new PositionResultDto
        {
            Category = p.Category,
            Fen = p.Fen,
            BestMove = p.BestMove,
            WorstMove = p.WorstMove,
            Depth = p.Depth,
            AverageNodes = p.AverageNodes,
            TotalNodes = p.TotalNodes,
            AverageNps = p.AverageNps,
            BranchingFactor = p.BranchingFactor,
            IsCorrectMove = p.IsCorrectMove
        }).ToList();
    }

    public IEnumerable<AvailablePositionDto> GetAvailablePositions()
    {
        if (!File.Exists(_dbPath)) return Enumerable.Empty<AvailablePositionDto>();

        using var db = new TestDatabase(_dbPath);
        return db.GetAvailablePositions().Select(p => new AvailablePositionDto
        {
            Category = p.Category,
            Fen = p.Fen,
            BestMove = p.BestMove,
            WorstMove = p.WorstMove,
            Attempts = p.Attempts
        }).ToList();
    }

    public IEnumerable<PositionHistoryPointDto> GetPositionHistory(string fen, int limit = 500)
    {
        if (!File.Exists(_dbPath) || string.IsNullOrWhiteSpace(fen)) return Enumerable.Empty<PositionHistoryPointDto>();

        var effectiveLimit = Math.Clamp(limit, 1, 1000);
        using var db = new TestDatabase(_dbPath);
        var history = db.GetPositionHistory(fen, effectiveLimit);

        return history.Select(h => new PositionHistoryPointDto
        {
            EnginePath = h.EnginePath,
            EngineFolder = h.EngineFolder,
            CommitHash = h.CommitHash,
            CommittedAt = h.CommittedAt,
            Category = h.Category,
            BestMove = h.BestMove,
            WorstMove = h.WorstMove,
            Depth = h.Depth,
            AverageNodes = h.AverageNodes,
            TotalNodes = h.TotalNodes,
            AverageNps = h.AverageNps,
            BranchingFactor = h.BranchingFactor,
            IsCorrectMove = h.IsCorrectMove
        }).ToList();
    }
}

public record EngineSummaryDto
{
    public string EnginePath { get; init; } = string.Empty;
    public string EngineFolder { get; init; } = string.Empty;
    public string CommitHash { get; init; } = string.Empty;
    public DateTimeOffset? CommittedAt { get; init; }
    public int TotalPositions { get; init; }
    public int CorrectPositions { get; init; }
    public double CorrectPercentage { get; init; }
    public double AverageDepth { get; init; }
    public double AverageNodes { get; init; }
    public long TotalNodes { get; init; }
    public double AverageNps { get; init; }
    public double AverageBranchingFactor { get; init; }
}

public record PositionResultDto
{
    public string Category { get; init; } = string.Empty;
    public string Fen { get; init; } = string.Empty;
    public string BestMove { get; init; } = string.Empty;
    public string WorstMove { get; init; } = string.Empty;
    public int Depth { get; init; }
    public int AverageNodes { get; init; }
    public long TotalNodes { get; init; }
    public int AverageNps { get; init; }
    public double BranchingFactor { get; init; }
    public bool IsCorrectMove { get; init; }
}

public record AvailablePositionDto
{
    public string Category { get; init; } = string.Empty;
    public string Fen { get; init; } = string.Empty;
    public string BestMove { get; init; } = string.Empty;
    public string WorstMove { get; init; } = string.Empty;
    public int Attempts { get; init; }
}

public record PositionHistoryPointDto
{
    public string EnginePath { get; init; } = string.Empty;
    public string EngineFolder { get; init; } = string.Empty;
    public string CommitHash { get; init; } = string.Empty;
    public DateTimeOffset? CommittedAt { get; init; }
    public string Category { get; init; } = string.Empty;
    public string BestMove { get; init; } = string.Empty;
    public string WorstMove { get; init; } = string.Empty;
    public int Depth { get; init; }
    public int AverageNodes { get; init; }
    public long TotalNodes { get; init; }
    public int AverageNps { get; init; }
    public double BranchingFactor { get; init; }
    public bool IsCorrectMove { get; init; }
}
