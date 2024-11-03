using System.Collections.Specialized;
using System.Runtime.Intrinsics.X86;
using Lolbot.Core;

namespace Lolbot.Tests;

public class Utilities
{
    [TestCase(Piece.None, Color.None)]
    [TestCase(Piece.WhitePawn, Color.White)]
    [TestCase(Piece.WhiteKnight, Color.White)]
    [TestCase(Piece.WhiteBishop, Color.White)]
    [TestCase(Piece.WhiteRook, Color.White)]
    [TestCase(Piece.WhiteQueen, Color.White)]
    [TestCase(Piece.WhiteKing, Color.White)]
    [TestCase(Piece.BlackPawn, Color.Black)]
    [TestCase(Piece.BlackKnight, Color.Black)]
    [TestCase(Piece.BlackBishop, Color.Black)]
    [TestCase(Piece.BlackRook, Color.Black)]
    [TestCase(Piece.BlackQueen, Color.Black)]
    [TestCase(Piece.BlackKing, Color.Black)]
    public void Pieces_Should_Have_Color(Piece piece, Color color)
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

    [TestCase(Color.White, 27, ExpectedResult = 0x1c1c1c1c08000000ul)]
    [TestCase(Color.White, 15, ExpectedResult = 0xc0c0c0c0c0c08000ul)]
    [TestCase(Color.Black, 15, ExpectedResult = 0x80c0ul)]
    [TestCase(Color.Black, 0, ExpectedResult = 0ul)]
    [TestCase(Color.Black, 7, ExpectedResult = 0ul)]
    [TestCase(Color.White, 56, ExpectedResult = 0ul)]
    [TestCase(Color.White, 63, ExpectedResult = 0ul)]
    [TestCase(Color.White, 30, ExpectedResult = 0xe0e0e0e040000000ul)]
    [TestCase(Color.White, 33, ExpectedResult = 0x707070200000000ul)]
    public ulong PassedPawnMasks(Color color, byte index)
    {
        return MovePatterns.PassedPawnMasks[(int)color][index];
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