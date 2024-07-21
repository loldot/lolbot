using System.Diagnostics;
using Lolbot.Core;

namespace Lolbot.Tests;
public class Pawns
{
    [Test]
    public void Should_Be_Blocked_By_Opponent()
    {
        var position = Position.FromFen("8/8/6p1/6p1/1p4P1/1P3P2/8/8 w - - 0 1");

        var whiteMoves = position.GenerateLegalMoves(Piece.WhitePawn);

        whiteMoves.ToArray().Should().BeEquivalentTo([new Move('P', "f3", "f4")]);

        position = position with { CurrentPlayer = Color.Black };
        var blackMoves = position.GenerateLegalMoves('p');
        blackMoves.ToArray().Should().BeEmpty();
    }

    [Test]
    public void Should_Capture_En_Passant_Square()
    {
        var game = Engine.NewGame();
        game = Engine.Move(game, "e2", "e4");
        game = Engine.Move(game, "d7", "d5");
        game = Engine.Move(game, "f1", "e2");
        game = Engine.Move(game, "d5", "e4");
        game = Engine.Move(game, "f2", "f4");
        game = Engine.Move(game, "e4", "f3");

        var e4Pawn = game.CurrentPosition.BlackPawns & Squares.FromCoordinates("f4");
        e4Pawn.Should().Be(0);
    }

    [Test]
    public void Should_Not_Jump()
    {
        var position = Position.FromFen("r2k1nr1/5p2/2ppp3/8/8/3q4/PP1PNPPP/R1RQK3 w - - 0 1");
        var moves = position.GenerateLegalMoves('P').ToArray();
        moves.Should().NotContain(x => x == new Move('P', "d2", "d4"));
    }

    [Test]
    public void Should_Promote()
    {
        var position = Position.FromFen("3n3k/P7/8/8/8/8/8/K7 w - - 0 1");
        var moves = position.GenerateLegalMoves().ToArray();

        moves.Should().Contain(x => x.PromotionPiece == Piece.WhiteBishop);
        moves.Should().Contain(x => x.PromotionPiece == Piece.WhiteKnight);
        moves.Should().Contain(x => x.PromotionPiece == Piece.WhiteRook);
        moves.Should().Contain(x => x.PromotionPiece == Piece.WhiteQueen);
    }

    [Test]
    public void Promoting_Should_Update_Both_Pieces()
    {
        var position = Position.FromFen("1k6/4P3/8/8/8/8/8/1K6 w - - 0 1");
        var next = position.Move(Move.Promote('P', "e7", "e8", 'Q'));

        (next.WhitePawns & Squares.FromCoordinates("e7")).Should().Be(0);
        (next.WhiteQueens & Squares.FromCoordinates("e8")).Should().NotBe(0);
    }

    [Test]
    public void Promoting_Should_Not_Change_Oponent_Bitboard()
    {
        var position = Position.FromFen("1k6/4P3/8/8/8/8/8/1K6 w - - 0 1");
        var next = position.Move(Move.Promote('P', "e7", "e8", 'Q'));

        next.BlackPawns.Should().Be(0);
    }

    [Test]
    public void Pushing_Pawn_To_Last_Rank_Without_Promoting_Should_Not_Be_Allowed()
    {
        var position = Position.FromFen("8/7P/8/8/8/1rkr4/8/2K5 w - - 0 1");
        var moves = position.GenerateLegalMoves().ToArray();

        moves.Should().BeEquivalentTo([
            Move.Promote('P', "h7", "h8", 'B'),
            Move.Promote('P', "h7", "h8", 'N'),
            Move.Promote('P', "h7", "h8", 'R'),
            Move.Promote('P', "h7", "h8", 'Q')
        ]);
    }

    [Test]
    public void EnPassant_Should_Set_Biboards_Correctly()
    {
        var position = Position.FromFen("2k5/8/1pp2p2/3pPp2/3P1P2/1PP5/8/7K w - d6 0 1");
        var game = new Game(position, []);

        game = Engine.Move(game, "e5", "d6");

        game.CurrentPosition.WhitePawns
            .Should().Be(Bitboards.Create("b3", "c3", "d4", "d6", "f4"));

        game.CurrentPosition.BlackPawns
            .Should().Be(Bitboards.Create("b6", "c6", "f6", "f5"));

        var w = game.CurrentPosition.White;
        w.Should().Be(Bitboards.Create("b3", "c3", "d4", "d6", "f4", "h1"));
        var b = game.CurrentPosition.Black;
        b.Should().Be(Bitboards.Create("b6", "c6", "f6", "f5", "c8"));

        game.CurrentPosition.Occupied
            .Should().Be(b | w);
    }

    [Test]
    public void EnPassant_Should_Not_Count_As_Regular_Attack()
    {
        var position = Position.FromFen("2k5/8/1pp2p2/3pPp2/3P1P2/1PP5/8/7K w - d6 0 1");
        position.GenerateLegalMoves('P').ToArray()
            .Where(m => m.CaptureIndex == Squares.D5)
            .Should().HaveCount(1);
    }

    [Test]
    public void Should_Not_EnPassant_On_Square_0()
    {
        var position = Position.FromFen("5k2/8/8/8/8/8/1p6/R5K1 b - - 0 1");
        var movesTo0 = position.GenerateLegalMoves('p').ToArray()
            .Where(m => m.ToIndex == Squares.A1).ToArray();

        movesTo0

            .Should().HaveCount(4);
    }

    [Test]
    public void Promotion_Should_Set_Bitboards_Correctly()
    {
        var pos = Position.FromFen("1k6/pppPpppp/8/8/8/8/PPP1PPPP/1K6 w - - 0 1");
        pos = pos.Move(Move.Promote('P', "d7", "d8", 'Q'));

        var w_pawns = Bitboards.Create("a2", "b2", "c2", "e2", "f2", "g2", "h2");
        pos.WhitePawns.Should().Be(w_pawns);
        pos.WhiteQueens.Should().Be(Bitboards.Create("d8"));

        var w = w_pawns | Bitboards.Create("b1", "d8");
        pos.White.Should().Be(w);

        var b_pawns = Bitboards.Create("a7", "b7", "c7", "e7", "f7", "g7", "h7");
        pos.BlackPawns.Should().Be(b_pawns);
        var b = b_pawns | Bitboards.Create("b8");
        pos.Black.Should().Be(b);

        pos.Occupied.Should().Be(w | b);
    }

    [Test]
    public void Promotion_Capture_Should_Set_Bitboards_Correctly()
    {
        var pos = Position.FromFen("1k2b3/pppPpppp/8/8/8/8/PPP1PPPP/1K6 w - - 0 1");
        pos = pos.Move(Move.PromoteWithCapture('P', "d7", "e8", 'b', 'Q'));

        var w_pawns = Bitboards.Create("a2", "b2", "c2", "e2", "f2", "g2", "h2");
        pos.WhitePawns.Should().Be(w_pawns);
        pos.WhiteQueens.Should().Be(Bitboards.Create("e8"));

        var w = w_pawns | Bitboards.Create("b1", "e8");
        pos.White.Should().Be(w);

        var b_pawns = Bitboards.Create("a7", "b7", "c7", "e7", "f7", "g7", "h7");
        pos.BlackPawns.Should().Be(b_pawns);
        var b = b_pawns | Bitboards.Create("b8");
        pos.Black.Should().Be(b);

        pos.Occupied.Should().Be(w | b);
    }

    [Test]
    public void EnPassant_Is_Not_Allowed_When_Checking()
    {
        var pos = Position.FromFen("4k3/8/8/KpP4q/8/8/8/8 w - b6 0 1");
        pos.GenerateLegalMoves('P').ToArray()
            .Should().Equal([new Move('P', "c5", "c6")]);
    }


    [Test]
    public void EnPassant_Is_Not_Allowed_When_Pinned_By_Bishop()
    {
        var pos = Position.FromFen("2k5/8/b7/1Pp5/8/8/8/5K2 w - c6 0 1");
        pos.GenerateLegalMoves('P').ToArray()
            .Should().Equal([new Move('P', "b5", "a6", 'b')]);
    }
}