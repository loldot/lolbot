using System.Runtime.Intrinsics;
using Lolbot.Core;

namespace Lolbot.Tests;

[Category(TestSuites.Fast)]
public class Pins
{
    [TestCase("3k4/3r4/8/8/3R4/8/8/3K4 w - - 0 1", 5)]
    [TestCase("4k3/8/5q2/8/3R4/8/8/K7 w - - 0 1", 0)]
    [TestCase("4k3/8/5q2/8/3N4/2R5/8/K7 w - - 0 1", 14)]
    public void Should_Pin_Rook(string fen, int count)
    {
        var pos = MutablePosition.FromFen(fen);
        var moves = pos.GenerateLegalMoves('R');
        moves.Length.Should().Be(count);
    }

    [TestCase("4k3/8/5q2/8/8/2B5/8/K7 w - - 0 1", 4)]
    [TestCase("4k3/8/5q2/6B1/8/2B5/8/K7 w - - 0 1", 11)]
    public void Should_Pin_Bishop(string fen, int count)
    {
        var pos = MutablePosition.FromFen(fen);
        var moves = pos.GenerateLegalMoves('B');
        moves.Length.Should().Be(count);
    }

    [Test]
    public void Not_Pinned_When_X_Ray()
    {
        var pos = MutablePosition.FromFen("8/K1R1n1rk/8/8/8/8/8/8 w - - 0 1");
        var moves = pos.GenerateLegalMoves('R');
        var occ = pos.Occupied;
        var notPinned = MovePatterns.RookAttacks(Squares.C7, ref occ) & ~pos.White;
        moves.Length.Should().Be(Bitboards.CountOccupied(notPinned));
    }

    [Test]
    public void Pinned_With_Multiple_Pieces_Attacking_On_Pinmask()
    {
        var pos = MutablePosition.FromFen("3q4/K1R2r1k/8/3nb3/8/8/8/8 w - - 0 1");
        var moves = pos.GenerateLegalMoves('R');
        var occ = pos.Occupied;
        var pinnedMoves = MovePatterns.RookAttacks(Squares.C7, ref occ) & ~pos.White & Bitboards.Masks.GetRank(Squares.C7);
        moves.Length.Should().Be(Bitboards.CountOccupied(pinnedMoves));
    }

    [Test]
    public void Pinned_En_Passant()
    {
        var pos = MutablePosition.FromFen("2Q5/1K6/8/2pP4/4q3/5k2/8/8 w - c6 0 1");
        var moves = pos.GenerateLegalMoves('P').ToArray();
        moves.Should().OnlyContain(x => x.CaptureIndex == Squares.C5 && x.ToIndex == Squares.C6);
    }

    [Test]
    public void Pinned_En_Passant_Black()
    {
        var pos = MutablePosition.FromFen("8/Q7/8/3K4/3pP3/8/8/4q1k1 b - e3 0 1");
        var moves = pos.GenerateLegalMoves('p').ToArray();
        moves.Should().OnlyContain(x => x.CaptureIndex == Squares.E4 && x.ToIndex == Squares.E3);
    }

    [Test]
    public void Cannot_Block_Check_While_Pinned()
    {
        var pos = MutablePosition.FromFen("3k4/4r3/8/4N2b/8/8/4K3/8 w - - 0 1");
        var moves = pos.GenerateLegalMoves('N').ToArray();
        moves.Should().BeEmpty();
    }

    [Test]
    public void Pinned_Piece_Cannot_Escape_To_Another_Pin()
    {
        var pos = MutablePosition.FromFen("2q5/8/8/2P5/2K1Q1r1/8/8/8 w - - 0 1");
        var moves = pos.GenerateLegalMoves('Q').ToArray();
        moves.Should().OnlyContain(x => Bitboards.Masks.GetRank(x.ToIndex) == Bitboards.Masks.Rank_4);
    }

    [Test]
    public void Should_Not_Pin_When_Blocked()
    {
        var rookBetweenPawnAndKing = MutablePosition.FromFen("4k3/8/8/8/4r3/4P3/4R3/4K3 b - - 0 1");
        var noRook = MutablePosition.FromFen("4k3/8/8/8/4r3/4P3/8/4K3 b - - 0 1");

        var moves = rookBetweenPawnAndKing.GenerateLegalMoves().ToArray();


        moves.Should().BeEquivalentTo(noRook.GenerateLegalMoves().ToArray());
    }

    [Test]
    public void ManyPins()
    {
        var allPins = MutablePosition.FromFen("Q2R2Q1/8/2ppp3/R1pkp2R/2ppp3/8/B7/1K1R3Q b - - 0 1");

        ulong[] pins = [
            allPins.Pinmasks[0],
            allPins.Pinmasks[1],
            allPins.Pinmasks[2],
            allPins.Pinmasks[3]
        ];

        pins.Should().BeEquivalentTo([
            Bitboards.Create("a5","b5","c5","e5","f5","g5","h5"),
            Bitboards.Create("d1","d2","d3","d4","d6","d7","d8"),
            Bitboards.Create("a8","b7","c6","e4","f3","g2","h1"),
            Bitboards.Create("a2","b3","c4","e6","f7","g8")
        ]);

        allPins.GenerateLegalMoves().Length.Should().Be(1);
    }

    [Test]
    public void Should_Not_Pin_Behind_King()
    {
        var rookBetweenPawnAndKing = MutablePosition.FromFen("8/8/4n3/4k3/4p3/8/8/K3R3 b - - 0 1");
        var noRook = MutablePosition.FromFen("8/8/4n3/4k3/4p3/8/8/K7 b - - 0 1");

        var moves = rookBetweenPawnAndKing.GenerateLegalMoves().ToArray();


        moves.Should().BeEquivalentTo(noRook.GenerateLegalMoves().ToArray());
    }

    [Test]
    public void Should_Pin_When_Piece_On_Diagonal_After_King()
    {
        var expectedPinmask = Bitboards.Create("a4", "b5", "c6");
        var pos = MutablePosition.FromFen("4r3/3k4/8/1n6/Q7/8/8/1K6 b - - 0 1");


        ulong[] pins = [
            pos.Pinmasks[0],
            pos.Pinmasks[1],
            pos.Pinmasks[2],
            pos.Pinmasks[3]
        ];
        pins.ToArray().Should().Contain(expectedPinmask);
    }
    //3q3k/8/8/3P4/q1PKP2q/8/3N4/3q4 w - - 0 1

    [Test]
    public void PinnedPieceCannot_Capture_Checker()
    {
        var position = MutablePosition.FromFen("2k5/2q5/8/8/8/2N5/2Kp4/8 w - - 0 1");
        position.GenerateLegalMoves(Piece.WhiteKnight).ToArray().Should().BeEmpty();
    }

    [Test]
    public void Moving_Should_Update_Pinmasks()
    {
        var fen = "8/8/8/2k1rR2/3b4/2q5/2Q2B2/6K1 b - - 0 1";
        var position = MutablePosition.FromFen(fen);
        var game = new Game(position);
        
        Engine.Move(game, "d4", "f2");
        position.Pinmasks[0].Should().Be(0);
        position.Pinmasks[1].Should().Be(0);
        position.Pinmasks[2].Should().Be(0);
        position.Pinmasks[3].Should().Be(0);
        Engine.Move(game, "g1", "f2");

        position.Pinmasks[0].Should().Be(Bitboards.Create("d5", "e5", "f5"));
        position.Pinmasks[1].Should().Be(Bitboards.Create("c2", "c3", "c4"));
        position.Pinmasks[2].Should().Be(0);
        position.Pinmasks[3].Should().Be(0);
    }

    
    [Test]
    public void Undo_Should_Update_Pinmasks()
    {
        var fen = "8/8/8/2k1rR2/3b4/2q5/2Q2B2/6K1 b - - 0 1";
        var position = MutablePosition.FromFen(fen);
        var game = new Game(position);
        
        Engine.Move(game, "d4", "f2");
        Engine.Move(game, "g1", "f2");

        game.UndoLastMove();
        game.UndoLastMove();

        position.Pinmasks[0].Should().Be(Bitboards.Create("d5", "e5", "f5"));
        position.Pinmasks[1].Should().Be(Bitboards.Create("c2", "c3", "c4"));
        position.Pinmasks[2].Should().Be(Bitboards.Create("d4", "e3", "f2"));
        position.Pinmasks[3].Should().Be(0);
    }
}