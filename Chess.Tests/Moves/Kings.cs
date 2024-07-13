using Lolbot.Core;

namespace Lolbot.Tests;

class Kings
{
    [Test]
    public void White_Should_Be_Allowed_Castle()
    {
        var pos = Position.FromFen("r3k2r/p6p/8/8/8/8/P6P/R3K2R w KQkq - 0 1");
        var moves = pos.GenerateLegalMoves('K').ToArray();

        moves.Should().Contain(Move.QueenSideCastle(Color.White));
        moves.Should().Contain(Move.Castle(Color.White));
    }

    [Test]
    public void Black_Should_Be_Allowed_Castle()
    {
        var pos = Position.FromFen("r3k2r/p6p/8/8/8/8/P6P/R3K2R b KQkq - 0 1");
        var moves = pos.GenerateLegalMoves('k').ToArray();

        moves.Should().Contain(Move.QueenSideCastle(Color.Black));
        moves.Should().Contain(Move.Castle(Color.Black));
    }

    [Test]
    public void White_Should_Not_Be_Allowed_Blacks_Castling()
    {
        var pos = Position.FromFen("r3k3/p6p/8/8/8/8/P6P/R3K2R w KQq - 0 1");
        var moves = pos.GenerateLegalMoves('K').ToArray();

        moves.Should().NotContain(Move.QueenSideCastle(Color.Black));
        moves.Should().NotContain(Move.Castle(Color.Black));
    }

    [Test]
    public void White_Should_Not_Be_Allowed_Castling_When_Checked()
    {
        var pos = Position.FromFen("r3k3/p6p/8/8/8/8/P4p1P/R3K2R w KQq - 0 1");
        var moves = pos.GenerateLegalMoves('K').ToArray();

        moves.Should().NotContain(Move.QueenSideCastle(Color.White));
        moves.Should().NotContain(Move.Castle(Color.White));
    }

    [Test]
    public void Move_King_Should_Remove_Castling_Rights()
    {
        var pos = Position.FromFen("r3k2r/p6p/8/8/8/8/P6P/R3K2R w KQkq - 0 1");
        var game = new Game(pos, []);
        game.CurrentPosition.CastlingRights.Should().Be(CastlingRights.All);
        game = Engine.Move(game, "e1", "e2");

        game.CurrentPosition.CastlingRights.Should().NotHaveFlag(CastlingRights.WhiteQueen);
        game.CurrentPosition.CastlingRights.Should().NotHaveFlag(CastlingRights.WhiteKing);
    }

    [Test]
    public void AlmostAttacked_Castling()
    {
        var pos = Position.FromFen("r3k2r/8/8/8/8/3b4/8/R3K2R w KQkq - 0 1");
        pos.GenerateLegalMoves('K').ToArray().Should().BeEquivalentTo([
            Move.QueenSideCastle(Color.White),
            new Move("e1","d1"),
            new Move("e1","d2"),
            new Move("e1","f2")
        ]);
    }

    [Test]
    public void King_Side_Castle_Should_Set_Bitboards()
    {
        var pos = Position.FromFen("r1bqkb1r/1ppp1ppp/p1n2n2/4p3/B3P3/5N2/PPPP1PPP/RNBQK2R w KQkq - 2 5");
        var game = new Game(pos, []);
        game = Engine.Move(game, Move.Castle(Color.White));

        game.CurrentPosition.CastlingRights.Should().Be(CastlingRights.BlackKing | CastlingRights.BlackQueen);
        game.CurrentPosition.WhiteRooks.Should().Be(Bitboards.Create("A1", "F1"));
        game.CurrentPosition.WhiteKing.Should().Be(Bitboards.Create("G1"));
        
        var whiteRank1 = game.CurrentPosition.White & Bitboards.Masks.Rank_1;

        whiteRank1.Should().Be(Bitboards.Create("a1", "b1", "c1", "d1","f1", "g1"));
    }
    [Test]
    public async Task Rook_Should_Have_Legal_After_Castling()
    {
        var pgn = """
        
        1. e4 d5 2. Nf3 d4 3. Bc4 e5
        """;
        var (game, _) = await new PgnSerializer().Read(new StringReader(pgn));

        game.CurrentPosition.GenerateLegalMoves('K')
            .ToArray().Should().Contain(x => x.CastleIndex != 0);
        game = Engine.Move(game, Move.Castle(Color.White));

        game.CurrentPosition.GenerateLegalMoves('R')
            .ToArray().Should().HaveCount(1);

    }

    [Test]
    public async Task Rook_Should_Have_Legal_Moves_From_e1_After_Castle()
    {
        var pgn = """
        
        1. e4 d5 2. Nf3 d4 3. Bc4 e5 4. O-O
        """;
        var (game, _) = await new PgnSerializer().Read(new StringReader(pgn));
        game = Engine.Move(game, "c8", "e6");
        game = Engine.Move(game, "f1", "e1");

        game.CurrentPosition.GenerateLegalMoves('R')
            .ToArray().Should().NotBeEmpty();
    }
}