using Chess.Api;
namespace Chess.Tests;

public class Tests
{
    [Test]
    [TestCase("A1", 1ul)]
    [TestCase("A8", 1ul << 7)]
    [TestCase("D4", 1ul << 27)]
    [TestCase("D5", 1ul << 28)]
    [TestCase("E4", 1ul << 35)]
    [TestCase("E5", 1ul << 36)]
    [TestCase("H1", 1ul << 56)]
    [TestCase("H8", 1ul << 63)]
    public void SquareFromCoordinates(string coordinate, ulong value)
    {
        var square = Utils.SquareFromCoordinates(coordinate);
        square.Should().Be(value);
    }


    [Test]
    [TestCase("A2", 1ul << 1)]
    [TestCase("B3", 1ul << 10)]
    [TestCase("C4", 1ul << 19)]
    [TestCase("D5", 1ul << 28)]
    [TestCase("E6", 1ul << 37)]
    [TestCase("F7", 1ul << 46)]
    [TestCase("G3", 1ul << 50)]
    [TestCase("H8", 1ul << 63)]
    public void GetFilAndRankFromSquare(string coordinate, ulong square)
    {
        Utils.GetFile(square).Should().Be(coordinate[0]);
        Utils.GetRank(square).Should().Be(byte.Parse("" + coordinate[1]));
    }
}