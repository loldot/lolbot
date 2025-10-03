namespace Chess.Api.Testing;

public class TestPosition
{
    public string Name { get; set; } = string.Empty;
    public string Fen { get; set; } = string.Empty;
    public string? ExpectedBestMoveUci { get; set; }
    public string Category { get; set; } = string.Empty;
}

public static class TestPositions
{
    public static List<TestPosition> GetAllPositions() => new()
    {
        new() { Name = "CCC_Rxe6", Fen = "2rqk2r/pb1nbp1p/4p1p1/1B1n4/Np1N4/7Q/PP3PPP/R1B1R1K1 w kq - 0 1", ExpectedBestMoveUci = "e1e6", Category = "CCC" },
        new() { Name = "CCC_Bxg7", Fen = "r1bq1rk1/3nbppp/p2pp3/6PQ/1p1BP2P/2NB4/PPP2P2/2KR3R w - - 0 1", ExpectedBestMoveUci = "d3g7", Category = "CCC" },
        new() { Name = "CCC_Ng4", Fen = "2kr4/ppq2pp1/2b1pn2/2P4r/2P5/3BQN1P/P4PP1/R4RK1 b - - 0 1", ExpectedBestMoveUci = "f6g4", Category = "CCC" },
        new() { Name = "CCC_Nxf7", Fen = "r1bqr1k1/pp1n1ppp/5b2/4N1B1/3p3P/8/PPPQ1PP1/2K1RB1R w - - 0 1", ExpectedBestMoveUci = "e5f7", Category = "CCC" }
    };
}