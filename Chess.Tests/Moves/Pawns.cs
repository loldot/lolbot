using System.Diagnostics;
using Lolbot.Core;

namespace Lolbot.Tests;
public class Pawns
{
    [Test]
    public void Should_Be_Blocked_By_Opponent()
    {
        var position = Position.FromFen("8/8/6p1/6p1/1p4P1/1P3P2/8/8 w - - 0 1");

        var blackMoves = position.GenerateLegalMoves(Color.Black, Piece.BlackPawn);
        var whiteMoves = position.GenerateLegalMoves(Color.White, Piece.WhitePawn);

        whiteMoves.ToArray().Should().BeEquivalentTo([new Move("f3", "f4")]);
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
        moves.Should().NotContain(x => x == new Move("d2", "d4"));
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
        var next = position.Move(new Move("e7", "e8") with { PromotionPiece = Piece.WhiteQueen });

        (next.WhitePawns & Squares.FromCoordinates("e7")).Should().Be(0);
        (next.WhiteQueens & Squares.FromCoordinates("e8")).Should().NotBe(0);
    }

    [Test]
    public void Promoting_Should_Not_Change_Oponent_Bitboard()
    {
        var position = Position.FromFen("1k6/4P3/8/8/8/8/8/1K6 w - - 0 1");
        var next = position.Move(new Move("e7", "e8") with { PromotionPiece = Piece.WhiteQueen });

        next.BlackPawns.Should().Be(0);
    }

    [Test]
    public void Pushing_Pawn_To_Last_Rank_Without_Promoting_Should_Not_Be_Allowed()
    {
        var position = Position.FromFen("8/7P/8/8/8/1rkr4/8/2K5 w - - 0 1");
        var moves = position.GenerateLegalMoves().ToArray();

        moves.Should().BeEquivalentTo([
            new Move("h7", "h8") with { PromotionPiece = Piece.WhiteBishop },
            new Move("h7", "h8") with { PromotionPiece = Piece.WhiteKnight },
            new Move("h7", "h8") with { PromotionPiece = Piece.WhiteRook },
            new Move("h7", "h8") with { PromotionPiece = Piece.WhiteQueen }
        ]);
    }

    [Test]
    public void EnPassant_Should_Set_Biboards_Correctly()
    {
        var position = Position.FromFen("2k5/8/1pp2p2/3pPp2/3P1P2/1PP5/8/7K w - d6 0 1");
        var game = new Game(position, []);

        game = Engine.Move(game, "e5", "d6");

        game.CurrentPosition.WhitePawns
            .Should().Be(Bitboards.Create("b3","c3", "d4", "d6", "f4"));
        
        game.CurrentPosition.BlackPawns
            .Should().Be(Bitboards.Create("b6", "c6", "f6", "f5"));

        var w = game.CurrentPosition.White;
            w.Should().Be(Bitboards.Create("b3","c3", "d4", "d6", "f4", "h1"));
        var b = game.CurrentPosition.Black;
            b.Should().Be(Bitboards.Create("b6", "c6", "f6", "f5", "c8"));

        game.CurrentPosition.Occupied
            .Should().Be(b | w);
    }

    [Test]
    public void Promotion_Should_Set_Bitboards_Correctly()
    {
        var pos = Position.FromFen("1k6/pppPpppp/8/8/8/8/PPP1PPPP/1K6 w - - 0 1");
        pos = pos.Move(new Move("d7", "d8") with { PromotionPiece = Piece.WhiteQueen});

        var w_pawns = Bitboards.Create("a2", "b2", "c2", "e2", "f2", "g2", "h2");
        pos.WhitePawns.Should().Be(w_pawns);
        pos.WhiteQueens.Should().Be(Bitboards.Create("d8"));
        
        var w = w_pawns | Bitboards.Create("b1", "d8");
        pos.White.Should().Be(w);

        var b_pawns = Bitboards.Create("a7","b7","c7","e7","f7","g7","h7");
        pos.BlackPawns.Should().Be(b_pawns);
        var b = b_pawns | Bitboards.Create("b8");
        pos.Black.Should().Be(b);

        pos.Occupied.Should().Be(w | b);
    }

    [Test]
    public void Promotion_Capture_Should_Set_Bitboards_Correctly()
    {
        var pos = Position.FromFen("1k2b3/pppPpppp/8/8/8/8/PPP1PPPP/1K6 w - - 0 1");
        pos = pos.Move(new Move("d7", "e8", "e8", 'b') with { PromotionPiece = Piece.WhiteQueen});

        var w_pawns = Bitboards.Create("a2", "b2", "c2", "e2", "f2", "g2", "h2");
        pos.WhitePawns.Should().Be(w_pawns);
        pos.WhiteQueens.Should().Be(Bitboards.Create("e8"));
        
        var w = w_pawns | Bitboards.Create("b1", "e8");
        pos.White.Should().Be(w);

        var b_pawns = Bitboards.Create("a7","b7","c7","e7","f7","g7","h7");
        pos.BlackPawns.Should().Be(b_pawns);
        var b = b_pawns | Bitboards.Create("b8");
        pos.Black.Should().Be(b);

        pos.Occupied.Should().Be(w | b);
    }
}