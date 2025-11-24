using System.Diagnostics;
using Lolbot.Core;
namespace Lolbot.Tests;

[Category(TestSuites.Fast)]
public class HashTable
{
    private static TranspositionTable tt = new TranspositionTable();

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
        var bestMove = new Move(Piece.WhitePawn, Squares.A2, Squares.A4);

        tt.Add(x, 1, 199, TranspositionTable.Exact, bestMove);

        var entry = tt.Get(x);
        entry.IsSet.Should().BeTrue();
        entry.Evaluation.Should().Be(199);
        entry.Move.Should().Be(bestMove);
    }


    [Test]
    public void Should_Not_Find_Move_Not_Added()
    {
        ulong addedHash = 0x1337_d3ad_b33f_0f13;
        ulong missingHash = 0x2337_d3ad_b33f_0f13;

        tt.Add(addedHash, 1, 199, TranspositionTable.Exact, new Move());

        var found = tt.TryGet(missingHash, 1, out var _);
        found.Should().BeFalse();
    }


    [Test]
    public void Should_Always_Replace()
    {
        ulong hash = 0x1337_d3ad_b33f_0f13;

        var bestMove = new Move('P', "e2", "e4");

        tt.Add(hash, 1, 199, TranspositionTable.Exact, new Move('N', "e1", "f3"));
        tt.Add(hash, 11, 15, TranspositionTable.Exact, bestMove);

        tt.TryGet(hash, 1, out var entry);
        var expectedEntry = new TranspositionTable.Entry(hash, 11, 15, TranspositionTable.Exact, bestMove);
        entry.Should().BeEquivalentTo(expectedEntry);
    }

    [Test]
    public void CanAddBigHash()
    {
        tt.Add(ulong.MaxValue, 55, 1000, TranspositionTable.UpperBound, new Move());
        tt.Get(ulong.MaxValue).Evaluation.Should().Be(1000);
    }

    // [Test]
    // public void CanAddMate()
    // {
    //     tt.Add(12345, 2, Search.Mate, TranspositionTable.Exact, new Move());
    //     tt.Add(12345, 2, -Search.Mate, TranspositionTable.Exact, new Move());
    // }

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