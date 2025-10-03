namespace Chess.Api.Services;

public class TestResultsService
{
    private readonly string _dbPath;
    public TestResultsService(string dbPath)
    {
        _dbPath = dbPath;
    }

    public IEnumerable<EngineSummaryDto> GetEngineSummaries()
    {
        if (!File.Exists(_dbPath)) return Enumerable.Empty<EngineSummaryDto>();
    using var db = new TestDatabase(_dbPath);
        var reports = db.GetTopPerformingEngines(50);
        return reports.Select(r => new EngineSummaryDto
        {
            CommitHash = r.CommitHash,
            TotalPositions = r.TotalPositions,
            CorrectPositions = r.CorrectPositions,
            CorrectPercentage = r.CorrectPercentage,
            AverageDepth = r.AverageDepth,
            AverageNodes = r.AverageNodes,
            AverageNps = r.AverageNps,
            AverageTimeMs = r.AverageTimeMs,
            LatestTestTime = r.LatestTestTime
        }).ToList();
    }

    public IEnumerable<PositionResultDto>? GetPositionResults(string commitHash)
    {
        if (!File.Exists(_dbPath)) return null;
    using var db = new TestDatabase(_dbPath);
        var positions = db.GetPositionPerformance(commitHash);
        if (positions.Count == 0) return null;
        return positions.Select(p => new PositionResultDto
        {
            PositionName = p.PositionName,
            Fen = p.Fen,
            BestMove = p.BestMove,
            ActualDepth = p.ActualDepth,
            Nodes = p.Nodes,
            Nps = p.Nps,
            TimeMs = p.TimeMs,
            ScoreCp = p.ScoreCp,
            ScoreMate = p.ScoreMate,
            PrincipalVariation = p.PrincipalVariation,
            IsCorrectMove = p.IsCorrectMove
        }).ToList();
    }
}

public record EngineSummaryDto
{
    public string CommitHash { get; init; } = string.Empty;
    public int TotalPositions { get; init; }
    public int CorrectPositions { get; init; }
    public double CorrectPercentage { get; init; }
    public double AverageDepth { get; init; }
    public long AverageNodes { get; init; }
    public long AverageNps { get; init; }
    public double AverageTimeMs { get; init; }
    public DateTime LatestTestTime { get; init; }
}

public record PositionResultDto
{
    public string PositionName { get; init; } = string.Empty;
    public string Fen { get; init; } = string.Empty;
    public string BestMove { get; init; } = string.Empty;
    public int ActualDepth { get; init; }
    public long Nodes { get; init; }
    public long Nps { get; init; }
    public int TimeMs { get; init; }
    public int? ScoreCp { get; init; }
    public int? ScoreMate { get; init; }
    public string PrincipalVariation { get; init; } = string.Empty;
    public bool IsCorrectMove { get; init; }
}
