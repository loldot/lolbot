using System.Collections.Specialized;
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
    public unsafe void MoveSize()
    {
        sizeof(MM).Should().Be(4);
    }


    [Test]
    public void EP()
    {
        var position = new Position()
            .Move(new Move(11, 27));
        position.EnPassant.Should().Be(19);
    }

    [Test]
    public void NEP()
    {
        var position = new Position()
            .Move(new Move(11, 19));
        position.EnPassant.Should().Be(0);
    }

    [Test]
    public void EP_Bl()
    {
        var position = new Position()
            .Move(new Move(51, 35));
        position.EnPassant.Should().Be(43);
    }

    public readonly struct MM
    {
        public static BitVector32.Section A = BitVector32.CreateSection(6);
        public static BitVector32.Section B = BitVector32.CreateSection(6, A);
        public static BitVector32.Section C = BitVector32.CreateSection(6, B);
        public static BitVector32.Section D = BitVector32.CreateSection(6, C);

        public readonly BitVector32 X;
        public MM(byte x, byte y, byte z, byte f)
        {
            X[A] = x;
            X[B] = y;
            X[C] = z;
            X[D] = f;
        }
    }
}