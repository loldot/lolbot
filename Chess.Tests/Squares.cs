using System.Numerics;
using Chess.Api;
namespace Chess.Tests;

public class Tests
{
    [Test]
    [TestCase("a1", 1ul)]
    [TestCase("a8", 1ul << 56)]
    [TestCase("d4", 1ul << 27)]
    [TestCase("d5", 1ul << 35)]
    [TestCase("e2", 1ul << 12)]
    [TestCase("e4", 1ul << 28)]
    [TestCase("e5", 1ul << 36)]
    [TestCase("h1", 1ul << 7)]
    [TestCase("f3", 1ul << 21)]
    [TestCase("h8", 1ul << 63)]
    public void SquareFromCoordinates(string coordinate, ulong value)
    {
        var square = Utils.SquareFromCoordinates(coordinate);
        square.Should().Be(value);
    }


    [Test]
    [TestCase("a2", 1ul << 8)]
    [TestCase("b3", 1ul << 17)]
    [TestCase("c4", 1ul << 26)]
    [TestCase("d5", 1ul << 35)]
    [TestCase("e6", 1ul << 44)]
    [TestCase("f7", 1ul << 53)]
    [TestCase("g3", 1ul << 22)]
    [TestCase("h8", 1ul << 63)]
    public void GetFilAndRankFromSquare(string coordinate, ulong square)
    {
        Utils.GetFile(square).Should().Be(coordinate[0]);
        Utils.GetRank(square).Should().Be(byte.Parse("" + coordinate[1]));
    }
}