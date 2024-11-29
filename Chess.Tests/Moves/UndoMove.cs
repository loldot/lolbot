using Lolbot.Core;

namespace Lolbot.Tests;

[Category(TestSuites.Fast)]
public class UndoMove
{
    [Test]
    public void Move_Set_Ep()
    {
        string fen = "rnbqkbnr/ppp1pppp/8/3pP3/8/8/PPPP1PPP/RNBQKBNR b KQkq - 0 2";
        var pos = MutablePosition.FromFen(fen);
        var doublePush = new Move('p', "f7", "f5");

        pos.Move(in doublePush);
        pos.EnPassant.Should().Be(Squares.IndexFromCoordinate("f6"));
    }
    [Test]
    public void Undo_Should_Set_EnPassant_Square()
    {
        var fen = "rnbqkbnr/ppp1p1pp/8/3pPp2/8/8/PPPP1PPP/RNBQKBNR w KQkq f6 0 3";
        var pos = MutablePosition.FromFen(fen);

        var randomMove = new Move('N', "g1", "f3");

        pos.Move(in randomMove);
        pos.Undo(in randomMove);

        pos.EnPassant.Should().Be(Squares.IndexFromCoordinate("f6"));
    }

    [Test]
    public void Undo_Should_Handle_Ep_Capture()
    {

        var fen = "rnbqkbnr/ppp1p1pp/8/3pPp2/8/8/PPPP1PPP/RNBQKBNR w KQkq f6 0 3";
        var pos = MutablePosition.FromFen(fen);

        var epCapture = new Move(
            Piece.WhitePawn,
            Squares.IndexFromCoordinate("d5"),
            Squares.IndexFromCoordinate("f6"), 
            Piece.BlackPawn,
            Squares.IndexFromCoordinate("f5")
        );

        pos.Move(in epCapture);
        pos.Undo(in epCapture);
        
        pos.BlackPawns.Should().Be(Bitboards.Create("a7","b7", "c7", "d5", "e7", "f5", "g7", "h7"));
        pos.WhitePawns.Should().Be(Bitboards.Create("a2", "b2", "c2", "d2", "e5", "f2", "g2", "h2"));
        pos.EnPassant.Should().Be(Squares.IndexFromCoordinate("f6"));
    }

    [Test]
    public void Undo_Should_Handle_Promotion_With_Capture()
    {
        var fen = "rnbq1bnr/pppP1kpp/4p3/5p2/8/8/PPPP1PPP/RNBQKBNR w KQ - 1 5";
        var pos = MutablePosition.FromFen(fen);

        var promotion = new Move(
            Piece.WhitePawn,
            Squares.IndexFromCoordinate("d7"),
            Squares.IndexFromCoordinate("d8"),
            Piece.BlackBishop,
            Piece.WhiteQueen);

        pos.Move(in promotion);
        pos.Undo(in promotion);

        pos.WhiteQueens.Should().Be(Bitboards.Create("d1"));
        pos.BlackBishops.Should().Be(Bitboards.Create("c8", "f8"));        
        pos.WhitePawns.Should().Be(Bitboards.Create("a2", "b2", "c2", "d7", "d2", "f2", "g2", "h2"));
    }

    [Test]
    public void Undo_Should_Reset_CastlingRights()
    {
        var fen = "rnbqkbnr/ppppp1pp/8/5p2/4P3/8/PPPP1PPP/RNBQKBNR w KQkq - 0 2";
        var pos = MutablePosition.FromFen(fen);
        var kingMove = new Move('K', "e1", "e2");
        
        pos.Move(in kingMove);
        pos.Undo(in kingMove);

        pos.CastlingRights.Should().Be(CastlingRights.All);
    } 
}