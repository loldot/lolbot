using Lolbot.Core;
namespace Lolbot.Tests;

public class HashTable
{
    private static TranspositionTable tt = new TranspositionTable();

    [Test]
    public void Should_Add_Move()
    {
        ulong x = 0x1337_d3ad_b33f_0f13;
        var m = new Move(Piece.WhitePawn, Squares.A2, Squares.A4);
        var entry = tt.Add(x, 1, 199, -109, m);

        entry.IsSet.Should().BeTrue();
        entry.Alpha.Should().Be(199);
        entry.Beta.Should().Be(-109);
        entry.BestMove.Should().Be(m);
    }


    [Test]
    public void Should_Find_Move()
    {
        ulong x = 0x1337_d3ad_b33f_0f13;
        var m = new Move(Piece.WhitePawn, Squares.A2, Squares.A4);

        tt.Add(x, 1, 199, -109, m);

        var entry = tt.Get(x);
        entry.IsSet.Should().BeTrue();
        entry.Alpha.Should().Be(199);
        entry.Beta.Should().Be(-109);
        entry.BestMove.Should().Be(m);
    }


    [Test]
    public void Should_Not_Find_Move_Not_Added()
    {
        ulong x = 0x1337_d3ad_b33f_0f13;
        ulong y = 0x2337_d3ad_b33f_0f13;

        var m = new Move(Piece.WhitePawn, Squares.A2, Squares.A4);

        tt.Add(x, 1, 199, -109, m);

        var entry = tt.Get(y);
        entry.IsSet.Should().BeFalse();
    }
}