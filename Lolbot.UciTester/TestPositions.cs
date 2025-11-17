namespace Lolbot.UciTester;

public record TestPosition(
    string Name,
    string Fen,
    string Category,
    string? ExpectedBestMove = null,
    string? ExpectedBestMoveUci = null,
    string? WorstMove = null);

public static class TestPositions
{
    public static List<TestPosition> GetAllPositions()
    {
        var positions = new List<TestPosition>();
        
        // Add CCC test positions only
        positions.AddRange(GetCccPositions());
        
        return positions;
    }

    private static List<TestPosition> GetCccPositions()
    {
        return new List<TestPosition>
        {
            new(
                Name: "CCC_Rxe6",
                Fen: "2rqk2r/pb1nbp1p/4p1p1/1B1n4/Np1N4/7Q/PP3PPP/R1B1R1K1 w kq - 0 1",
                Category: "CCC",
                ExpectedBestMove: "Rxe6",
                ExpectedBestMoveUci: "e1e6"),
            new(
                Name: "CCC_Bxg7",
                Fen: "r1bq1rk1/3nbppp/p2pp3/6PQ/1p1BP2P/2NB4/PPP2P2/2KR3R w - - 0 1",
                Category: "CCC",
                ExpectedBestMove: "Bxg7",
                ExpectedBestMoveUci: "d3g7"),
            new(
                Name: "CCC_Ng4",
                Fen: "2kr4/ppq2pp1/2b1pn2/2P4r/2P5/3BQN1P/P4PP1/R4RK1 b - - 0 1",
                Category: "CCC",
                ExpectedBestMove: "Ng4",
                ExpectedBestMoveUci: "f6g4"),
            new(
                Name: "CCC_Nxf7",
                Fen: "r1bqr1k1/pp1n1ppp/5b2/4N1B1/3p3P/8/PPPQ1PP1/2K1RB1R w - - 0 1",
                Category: "CCC",
                ExpectedBestMove: "Nxf7",
                ExpectedBestMoveUci: "e5f7"),
            new(
                Name: "CCC_Rc3",
                Fen: "3r4/2r5/p3nkp1/1p3p2/1P1pbP2/P2B3R/2PRN1P1/6K1 b - - 0 1",
                Category: "CCC",
                ExpectedBestMove: "Rc3",
                ExpectedBestMoveUci: "c7c3"),
            new(
                Name: "CCC_Rh3",
                Fen: "3b4/p3P1q1/P1n2pr1/4p3/2B1n1Pk/1P1R4/P1p3KN/1N6 w - - 0 1",
                Category: "CCC",
                ExpectedBestMove: "Rh3",
                ExpectedBestMoveUci: "d3h3"),
            new(
                Name: "CCC_Bd4",
                Fen: "7r/8/pB1p1R2/4k2q/1p6/1Pr5/P5Q1/6K1 w - - 0 1",
                Category: "CCC",
                ExpectedBestMove: "Bd4",
                ExpectedBestMoveUci: "b6d4"),
            new(
                Name: "CCC_Rxh7",
                Fen: "3r1r1k/1b4pp/ppn1p3/4Pp1R/Pn5P/3P4/4QP2/1qB1NKR1 w - - 0 1",
                Category: "CCC",
                ExpectedBestMove: "Rxh7",
                ExpectedBestMoveUci: "h5h7"),
            new(
                Name: "CCC_Qxf3",
                Fen: "1k2r2r/pbb2p2/2qn2p1/8/PP6/2P2N2/1Q2NPB1/R4RK1 b - - 0 1",
                Category: "CCC",
                ExpectedBestMove: "Qxf3",
                ExpectedBestMoveUci: "c6f3"),
            new(
                Name: "CCC_b3",
                Fen: "r6k/6R1/p4p1p/2p2P1P/1pq1PN2/6P1/1PP5/2KR4 w - - 0 1",
                Category: "CCC",
                ExpectedBestMove: "b3",
                ExpectedBestMoveUci: "b2b3"),
            new(
                Name: "CCC_Avoid_Qxh6",
                Fen: "6k1/p3b1np/6pr/6P1/1B2p2Q/K7/7P/8 w - - 0 1",
                Category: "CCC_Avoid",
                WorstMove: "Qxh6")
        };
    }
}