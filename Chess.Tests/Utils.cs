using System.Runtime.Intrinsics.X86;
using Lolbot.Core;

namespace Lolbot.Tests;

public class Utilities
{
    [TestCase(Piece.WhitePawn, Colors.White)]
    [TestCase(Piece.WhiteKnight, Colors.White)]
    [TestCase(Piece.WhiteBishop, Colors.White)]
    [TestCase(Piece.WhiteRook, Colors.White)]
    [TestCase(Piece.WhiteQueen, Colors.White)]
    [TestCase(Piece.WhiteKing, Colors.White)]
    [TestCase(Piece.BlackPawn, Colors.Black)]
    [TestCase(Piece.BlackKnight, Colors.Black)]
    [TestCase(Piece.BlackBishop, Colors.Black)]
    [TestCase(Piece.BlackRook, Colors.Black)]
    [TestCase(Piece.BlackQueen, Colors.Black)]
    [TestCase(Piece.BlackKing, Colors.Black)]
    public void Pieces_Should_Have_Color(Piece piece, Colors color)
    {
        Utils.GetColor(piece).Should().Be(color);
    }

    [Test]
    public void Test()
    {
        var position = new Position();
        var fromBlack = Bitboards.FlipAlongVertical(position.BlackPawns);

        fromBlack.Should().Be(position.WhitePawns);
    }

    [TestCase(Colors.White, 27, ExpectedResult = 0x1c1c1c1c08000000ul)]
    [TestCase(Colors.White, 15, ExpectedResult = 0xc0c0c0c0c0c08000ul)]
    [TestCase(Colors.Black, 15, ExpectedResult = 0x80c0ul)]
    [TestCase(Colors.Black, 0, ExpectedResult = 0ul)]
    [TestCase(Colors.Black, 7, ExpectedResult = 0ul)]
    [TestCase(Colors.White, 56, ExpectedResult = 0ul)]
    [TestCase(Colors.White, 63, ExpectedResult = 0ul)]
    [TestCase(Colors.White, 30, ExpectedResult = 0xe0e0e0e040000000ul)]
    [TestCase(Colors.White, 33, ExpectedResult = 0x707070200000000ul)]
    public ulong PassedPawnMasks(Colors color, byte index)
    {
        return MovePatterns.PassedPawnMasks[(int)color & 1][index];
    }

    [Test]
    public void HasPext()
    {
        Bmi2.IsSupported.Should().BeTrue();
    }

    [Test]
    public unsafe void MoveSize()
    {
        sizeof(Move).Should().Be(4);
    }
}