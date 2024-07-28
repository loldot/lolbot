using Lolbot.Core;
namespace Lolbot.Tests;

public class HashTable
{
    private static TranspositionTable tt = new TranspositionTable();

    [TestCase("rnbqkbnr/pppppppp/8/8/4P3/8/PPPP1PPP/RNBQKBNR b KQkq e3 0 1", ExpectedResult = 0x823c9b50fd114196ul)]
    [TestCase("rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1", ExpectedResult = 0x463b96181691fc9cul)]
    [TestCase("rnbq1bnr/ppp1pkpp/8/3pPp2/8/8/PPPPKPPP/RNBQ1BNR w - - 0 4", ExpectedResult = 0xfdd303c946bdd9ul)]
    [TestCase("7k/8/8/8/8/8/8/K7 w - - 0 1", ExpectedResult = 0x523760c49712209dul)]
    [TestCase("7k/8/8/8/8/8/8/K7 b - - 0 1", ExpectedResult = 0xaae1466e3835a594ul)]
    [TestCase("7k/8/8/2Pp4/8/8/8/K7 w - d6 0 1", ExpectedResult = 0x74e86a36565a2178ul)]
    [TestCase("8/8/8/3k4/8/2nK1N2/8/8 w - - 0 1", ExpectedResult = 0x02b70cb7a5dda169ul)]
    [TestCase("8/8/8/3k4/8/2nK1N2/8/8 b - - 0 1", ExpectedResult = 0xfa612a1d0afa2460)]
    public ulong ZobristKeys(string fen) => Position.FromFen(fen).Hash;

    [Test]
    public void Should_Add_Move()
    {
        ulong x = 0x1337_d3ad_b33f_0f13;
        var m = new Move(Piece.WhitePawn, Squares.A2, Squares.A4);
        var entry = tt.Add(x, 1, 199, TranspositionTable.Alpha, m);

        entry.IsSet.Should().BeTrue();
        entry.Evaluation.Should().Be(199);
    }


    [Test]
    public void Should_Find_Move()
    {
        ulong x = 0x1337_d3ad_b33f_0f13;
        var m = new Move(Piece.WhitePawn, Squares.A2, Squares.A4);

        tt.Add(x, 1, 199, TranspositionTable.Exact, m);

        var entry = tt.Get(x);
        entry.IsSet.Should().BeTrue();
        entry.Evaluation.Should().Be(199);
        entry.BestMove.Should().Be(m);
    }


    [Test]
    public void Should_Not_Find_Move_Not_Added()
    {
        ulong x = 0x1337_d3ad_b33f_0f13;
        ulong y = 0x2337_d3ad_b33f_0f13;

        var m = new Move(Piece.WhitePawn, Squares.A2, Squares.A4);

        tt.Add(x, 1, 199, TranspositionTable.Exact, m);

        var entry = tt.Get(y);
        entry.IsSet.Should().BeFalse();
    }
}