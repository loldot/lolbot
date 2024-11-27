using System.Diagnostics;
using Lolbot.Core;
namespace Lolbot.Tests;

[Category(TestSuites.Fast)]
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
    [TestCase("rnbqkbnr/ppp1p1pp/8/3pPp2/8/8/PPPPKPPP/RNBQ1BNR b kq - 0 3", ExpectedResult = 0x652a607ca3f242c1ul)]
    public ulong ZobristKeys(string fen) => Position.FromFen(fen).Hash;

    [Test]
    public void ZobristUpdates()
    {
        Move[] moves = [
            new Move('P', "e2", "e4"),
            new Move('p', "d7", "d5"),
            new Move('P', "e4", "e5"),
            new Move('p', "f7", "f5"),
            new Move('K', "e1", "e2"),
        ];

        ulong[] hashes = [
            0x823c9b50fd114196ul,
            0x0756b94461c50fb0ul,
            0x662fafb965db29d4ul,
            0x22a48b5a8e47ff78ul,
            0x652a607ca3f242c1ul,
        ];

        var position = new Position();
        for (int i = 0; i < moves.Length; i++)
        {
            position = position.Move(moves[i]);
            position.Hash.Should().Be(hashes[i]);
        }
    }

    [Test]
    public void Should_Add_Move()
    {
        ulong x = 0x1337_d3ad_b33f_0f13;
        var entry = tt.Add(x, 1, 199, TranspositionTable.LowerBound, new Move());

        entry.IsSet.Should().BeTrue();
        entry.Evaluation.Should().Be(199);
    }


    [Test]
    public void Should_Find_Move()
    {
        ulong x = 0x1337_d3ad_b33f_0f13;
        var m = new Move(Piece.WhitePawn, Squares.A2, Squares.A4);

        tt.Add(x, 1, 199, TranspositionTable.Exact, new Move());

        var entry = tt.Get(x);
        entry.IsSet.Should().BeTrue();
        entry.Evaluation.Should().Be(199);
    }


    [Test]
    public void Should_Not_Find_Move_Not_Added()
    {
        ulong x = 0x1337_d3ad_b33f_0f13;
        ulong y = 0x2337_d3ad_b33f_0f13;

        tt.Add(x, 1, 199, TranspositionTable.Exact, new Move());

        var found = tt.TryGet(y, 1, out var _);
        found.Should().BeFalse();
    }


    [Test]
    public void Should_Always_Replace()
    {
        ulong x = 0x1337_d3ad_b33f_0f13;

        tt.Add(x, 1, 199, TranspositionTable.Exact, new Move());
        tt.Add(x, 11, 15, TranspositionTable.Exact, new Move());


        tt.TryGet(x, 1, out var entry);
        entry.Should().BeEquivalentTo(new TranspositionTable.Entry(x, 11, 15, TranspositionTable.Exact, new Move()));
    }

    [Test]
    public void CanAddBigHash()
    {
        tt.Add(ulong.MaxValue, 55, 1000, TranspositionTable.UpperBound, new Move());
        tt.Get(ulong.MaxValue).Evaluation.Should().Be(1000);
    }

    [Test]
    public void CanAddMate()
    {
        tt.Add(12345, 2, Search.Mate, TranspositionTable.Exact, new Move());
        tt.Add(12345, 2, -Search.Mate, TranspositionTable.Exact, new Move());
    }

    [Test]
    public void Should_Be_Fast()
    {
        Random r = new Random();
        ulong[] keys = new ulong[ushort.MaxValue];
        for (int i = 0; i < ushort.MaxValue; i++)
        {
            keys[i] = (ulong)r.NextInt64();
        }

        var sw = Stopwatch.StartNew();
        for (int i = 0; i < ushort.MaxValue; i++)
        {
            tt.Add(keys[i], 11, 1337, TranspositionTable.Exact, new Move());
        }
        sw.Stop();
        Console.WriteLine($"ttwrite {ushort.MaxValue} entries {sw.ElapsedMilliseconds} ms");
        sw.ElapsedMilliseconds.Should().BeLessThan(50);

        for (int i = 0; i < ushort.MaxValue; i++)
        {
            keys[i] = (ulong)r.NextInt64();
        }

        sw.Restart();
        for (int i = 0; i < ushort.MaxValue; i++)
        {
            tt.TryGet(keys[i], 5, out var _);
        }
        Console.WriteLine($"ttread {ushort.MaxValue} entries {sw.ElapsedMilliseconds} ms");
        sw.ElapsedMilliseconds.Should().BeLessThan(50);
    }
}