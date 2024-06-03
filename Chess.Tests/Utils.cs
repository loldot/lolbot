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
}