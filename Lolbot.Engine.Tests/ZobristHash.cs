
using Lolbot.Core;

namespace Lolbot.Tests;

[Category(TestSuites.Fast)]
public class ZobristHash
{

    [TestCase("rnbqkbnr/pppppppp/8/8/4P3/8/PPPP1PPP/RNBQKBNR b KQkq e3 0 1", ExpectedResult = 0x823c9b50fd114196ul)]
    [TestCase("rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1", ExpectedResult = 0x463b96181691fc9cul)]
    [TestCase("rnbq1bnr/ppp1pkpp/8/3pPp2/8/8/PPPPKPPP/RNBQ1BNR w - - 0 4", ExpectedResult = 0xfdd303c946bdd9ul)]
    [TestCase("7k/8/8/8/8/8/8/K7 w - - 0 1", ExpectedResult = 0x523760c49712209dul)]
    [TestCase("7k/8/8/8/8/8/8/K7 b - - 0 1", ExpectedResult = 0xaae1466e3835a594ul)]
    [TestCase("7k/8/8/2Pp4/8/8/8/K7 w - d6 0 1", ExpectedResult = 0x74e86a36565a2178ul)]
    [TestCase("8/8/8/3k4/8/2nK1N2/8/8 w - - 0 1", ExpectedResult = 0x02b70cb7a5dda169ul)]
    [TestCase("8/8/8/3k4/8/2nK1N2/8/8 b - - 0 1", ExpectedResult = 0xfa612a1d0afa2460)]
    [TestCase("rnbqkbnr/ppp1p1pp/8/3pPp2/8/8/PPPPKPPP/RNBQ1BNR b kq - 0 3", ExpectedResult = 0x652a607ca3f242c1ul)]
    public ulong ZobristKeys(string fen) => MutablePosition.FromFen(fen).Hash;

    [Test]
    public void ZobristUpdates()
    {
        Move[] moves = [
            new Move('P', "e2", "e4"),
            new Move('p', "d7", "d5"),
            new Move('P', "e4", "e5"),
            new Move('p', "f7", "f5"),
            new Move('K', "e1", "e2"),
        ];

        ulong[] hashes = [
            0x823c9b50fd114196ul,
            0x0756b94461c50fb0ul,
            0x662fafb965db29d4ul,
            0x22a48b5a8e47ff78ul,
            0x652a607ca3f242c1ul,
        ];

        var position = new MutablePosition();
        for (int i = 0; i < moves.Length; i++)
        {
            position.Move(in moves[i]);
            position.Hash.Should().Be(hashes[i]);
        }
    }

    [Test]
    public void Incremental_Should_Update_Hash()
    {
        var position = new MutablePosition();

        var move = new Move('P', "e2", "e4");
        position.Move(in move);

        position.Hash.Should().Be(Hashes.New(position));
    }

    [Test]
    public void Incremental_Promotion_Should_Update_Hash()
    {
        var position = MutablePosition.FromFen("6r1/R4PBp/1p1b2k1/3P4/P1p1p3/7P/8/3N2K1 w - - 3 30");

        var move = new Move(Piece.WhitePawn, Squares.F7, Squares.F8, Piece.None, Piece.WhiteQueen);
        position.Move(in move);

        position.Hash.Should().Be(Hashes.New(position));
    }

    [Test]
    public void Incremental_Castling_Should_UpdateHash()
    {
        var position = MutablePosition.FromFen("r3k2r/pppppppp/8/8/8/8/4PPPP/3QK2R w Kkq - 0 1");

        position.Move(Move.Castle(Colors.White));
        position.Hash.Should().Be(Hashes.New(position));

        var bc = Move.QueenSideCastle(Colors.Black);
        position.Move(in bc);
        position.Hash.Should().Be(Hashes.New(position));

        position.Undo(in bc);
        position.Hash.Should().Be(Hashes.New(position));

        position.Move(Move.Castle(Colors.Black));
        position.Hash.Should().Be(Hashes.New(position));
    }


    [Test]
    public void Incremental_Promotion_With_Capture_Should_Update_Hash()
    {
        var position = MutablePosition.FromFen("6r1/R4PBp/1p1b2k1/3P4/P1p1p3/7P/8/3N2K1 w - - 3 30");

        var move = new Move(Piece.WhitePawn, Squares.F7, Squares.G8, Piece.BlackRook, Piece.WhiteQueen);
        position.Move(in move);

        position.Hash.Should().Be(Hashes.New(position));
    }

    [Test]
    public void Incremental_Remove_EP_Should_Update_Hash()
    {
        var pos = MutablePosition.FromFen("rnbqkb1r/pp1p1ppp/5n2/2pPp3/4P3/8/PPP2PPP/RNBQKBNR w KQkq c6 0 4");
        var move = new Move('N', "b1", "c3");
        pos.Move(in move);
        pos.Hash.Should().Be(Hashes.New(pos));
    }

    [Test]
    public void Incremental_Remove_EP_Should_Update_Hash_2()
    {
        var position = MutablePosition.FromFen("4r1k1/2p1n3/7P/1p1P2Pp/1P3p2/1P2pNB1/4P2P/K3R3 b - - 48 97");
        var move = new Move('p', "c7", "c5");
        position.Move(in move);

        position.Hash.Should().Be(Hashes.New(position));

        move = new Move('R', "e1", "d1");
        position.Move(in move);
        position.Hash.Should().Be(Hashes.New(position));
    }

    [Test]
    public void Incremental_Set_EP_Should_Update_Hash()
    {
        var pos = MutablePosition.FromFen("rnbqkb1r/pppppppp/5n2/3P4/4P3/8/PPP2PPP/RNBQKBNR b KQkq - 0 3");
        pos.Hash.Should().Be(0x5e39e5f677233e16);

        var move = new Move('p', "e7", "e5");
        pos.Move(in move);

        pos.Hash.Should().Be(Hashes.New(pos));

        var posWithoutEp = MutablePosition.FromFen("rnbqkb1r/pp1p1ppp/5n2/2pPp3/4P3/8/PPP2PPP/RNBQKBNR w KQkq - 0 4");
        pos.Hash.Should().NotBe(posWithoutEp.Hash);
    }

    [Test]
    public void Incremental_EnPassant_Capture_Should_Update_Hash()
    {
        // Position with EP available: black just played e7-e5; white pawn on d5 can capture e6 en passant
        var pos = MutablePosition.FromFen("rnbqkbnr/pppp1ppp/8/3Pp3/8/8/PPPP1PPP/RNBQKBNR w KQkq e6 0 3");

        var move = new Move(Piece.WhitePawn, Squares.D5, Squares.E6, Piece.BlackPawn, Squares.E5);
        pos.Move(in move);

        pos.Hash.Should().Be(Hashes.New(pos));
    }

    [Test]
    public void Incremental_EnPassant_Capture_Black_Should_Update_Hash()
    {
        // Mirror case: white just played d2-d4; black pawn on e4 can capture d3 en passant
        var pos = MutablePosition.FromFen("rnbqkbnr/ppp1pppp/8/8/3Pp3/8/PPP2PPP/RNBQKBNR b KQkq d3 0 2");

        var move = new Move(Piece.BlackPawn, Squares.E4, Squares.D3, Piece.WhitePawn, Squares.D4);
        pos.Move(in move);

        pos.Hash.Should().Be(Hashes.New(pos));
    }

    [Test]
    public void Incremental_DoublePush_Without_Adjacent_Pawn_Should_Not_Set_EP()
    {
        // From the start position, e2-e4 should not set EP (no black pawn on d4/f4)
        var pos = new MutablePosition();
        var move = new Move('P', "e2", "e4");
        pos.Move(in move);

        pos.EnPassant.Should().Be(0);
        pos.Hash.Should().Be(Hashes.New(pos));
    }

    [Test]
    public void Incremental_Underpromotion_NoCapture_Should_Update_Hash()
    {
        // White underpromotes on a8 without capture
        var pos = MutablePosition.FromFen("1k5r/P7/8/8/8/8/8/4K3 w - - 0 1");
        var move = new Move(Piece.WhitePawn, Squares.A7, Squares.A8, Piece.None, Piece.WhiteKnight);
        pos.Move(in move);

        pos.Hash.Should().Be(Hashes.New(pos));
    }

    [Test]
    public void Incremental_Underpromotion_With_Capture_Should_Update_Hash()
    {
        // White promotes on a8 with capture and underpromotion
        var pos = MutablePosition.FromFen("r5k1/1P6/8/8/8/8/8/4K3 w - - 0 1");
        var move = new Move(Piece.WhitePawn, Squares.B7, Squares.A8, Piece.BlackRook, Piece.WhiteBishop);
        pos.Move(in move);

        pos.Hash.Should().Be(Hashes.New(pos));
    }

    [Test]
    public void Incremental_Rook_Move_Should_Update_Castling_Rights_Hash()
    {
        // Moving the rook from h1 should remove white king-side castling right
        var pos = MutablePosition.FromFen("r3k2r/8/8/8/8/8/8/R3K2R w KQkq - 0 1");
        var move = new Move(Piece.WhiteRook, Squares.H1, Squares.H2);
        pos.Move(in move);

        pos.Hash.Should().Be(Hashes.New(pos));
    }

    [Test]
    public void Incremental_Capture_Rook_Should_Update_Castling_Rights_Hash()
    {
        // Capturing the rook on a8 should remove black queen-side castling right
        var pos = MutablePosition.FromFen("r3k2r/8/8/8/8/8/8/R3K2R w KQkq - 0 1");
        var move = new Move(Piece.WhiteRook, Squares.A1, Squares.A8, Piece.BlackRook);
        pos.Move(in move);

        pos.Hash.Should().Be(Hashes.New(pos));
    }

    [Test]
    public void Incremental_SkipTurn_And_Undo_Should_Update_Hash()
    {
        // Ensure null move toggles side-to-move (and clears EP if present) correctly
        var pos = MutablePosition.FromFen("rnbqkbnr/pppppppp/8/3Pp3/8/8/PPPP1PPP/RNBQKBNR w KQkq e6 0 3");
        var originalHash = pos.Hash;

        pos.SkipTurn();
        pos.Hash.Should().Be(Hashes.New(pos));

        pos.UndoSkipTurn();
        pos.Hash.Should().Be(originalHash);
        pos.Hash.Should().Be(Hashes.New(pos));
    }

    [Test]
    public void Incremental_Move_Then_Undo_Should_Restore_Hash()
    {
        var pos = new MutablePosition();
        var m1 = new Move('N', "g1", "f3");
        pos.Move(in m1);
        var hashAfter = pos.Hash;
        hashAfter.Should().Be(Hashes.New(pos));

        pos.Undo(in m1);
        pos.Hash.Should().Be(Hashes.New(pos));
    }

}