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
            EnginePath = r.EnginePath,
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

    public IEnumerable<PositionResultDto> GetPositionResults(string enginePath)
    {
        if (!File.Exists(_dbPath)) return Enumerable.Empty<PositionResultDto>();

        using var db = new TestDatabase(_dbPath);
        var positions = db.GetPositionPerformance(enginePath);
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
}

public record EngineSummaryDto
{
    public string EnginePath { get; init; } = string.Empty;
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
