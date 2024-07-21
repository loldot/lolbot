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

    [Test]
    public void EP()
    {
        var position = new Position()
            .Move(new Move(Piece.WhitePawn, 11, 27));
        position.EnPassant.Should().Be(19);
    }

    [Test]
    public void NEP()
    {
        var position = new Position()
            .Move(new Move(Piece.WhitePawn, 11, 19));
        position.EnPassant.Should().Be(0);
    }

    [Test]
    public void EP_Bl()
    {
        var position = new Position()
            .Move(new Move(Piece.BlackPawn, 51, 35));
        position.EnPassant.Should().Be(43);
    }

    [Test]
    public void HasPext()
    {
        Bmi2.IsSupported.Should().BeTrue();
    }
}