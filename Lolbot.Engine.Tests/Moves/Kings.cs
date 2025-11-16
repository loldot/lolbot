using Lolbot.Core;

namespace Lolbot.Tests;

[Category(TestSuites.Fast)]
public class Kings
{
    [Test]
    public void White_Should_Be_Allowed_Castle()
    {
        var pos = MutablePosition.FromFen("r3k2r/p6p/8/8/8/8/P6P/R3K2R w KQkq - 0 1");
        var moves = pos.GenerateLegalMoves('K').ToArray();

        moves.Should().Contain(Move.QueenSideCastle(Colors.White));
        moves.Should().Contain(Move.Castle(Colors.White));
    }

    [Test]
    public void Black_Should_Be_Allowed_Castle()
    {
        var pos = MutablePosition.FromFen("r3k2r/p6p/8/8/8/8/P6P/R3K2R b KQkq - 0 1");
        var moves = pos.GenerateLegalMoves('k').ToArray();

        moves.Should().Contain(Move.QueenSideCastle(Colors.Black));
        moves.Should().Contain(Move.Castle(Colors.Black));
    }

    [Test]
    public void White_Should_Not_Be_Allowed_Blacks_Castling()
    {
        var pos = MutablePosition.FromFen("r3k3/p6p/8/8/8/8/P6P/R3K2R w KQq - 0 1");
        var moves = pos.GenerateLegalMoves('K').ToArray();

        moves.Should().NotContain(Move.QueenSideCastle(Colors.Black));
        moves.Should().NotContain(Move.Castle(Colors.Black));
    }

    [Test]
    public void White_Should_Not_Be_Allowed_Castling_When_Checked()
    {
        var pos = MutablePosition.FromFen("r3k3/p6p/8/8/8/8/P4p1P/R3K2R w KQq - 0 1");
        var moves = pos.GenerateLegalMoves('K').ToArray();

        moves.Should().NotContain(Move.QueenSideCastle(Colors.White));
        moves.Should().NotContain(Move.Castle(Colors.White));
    }

    [Test]
    public void Move_King_Should_Remove_Castling_Rights()
    {
        var pos = MutablePosition.FromFen("r3k2r/p6p/8/8/8/8/P6P/R3K2R w KQkq - 0 1");
        pos.CastlingRights.Should().Be(CastlingRights.All);
        pos.Move(new Move('K', "e1", "e2"));

        pos.CastlingRights.Should().NotHaveFlag(CastlingRights.WhiteQueen);
        pos.CastlingRights.Should().NotHaveFlag(CastlingRights.WhiteKing);
    }

    [Test]
    public void AlmostAttacked_Castling()
    {
        var pos = MutablePosition.FromFen("r3k2r/8/8/8/8/3b4/8/R3K2R w KQkq - 0 1");
        pos.GenerateLegalMoves('K').ToArray().Should().BeEquivalentTo([
            Move.QueenSideCastle(Colors.White),
            new Move('K', "e1","d1"),
            new Move('K', "e1","d2"),
            new Move('K', "e1","f2")
        ]);
    }

    [Test]
    public void King_Side_Castle_Should_Set_Bitboards()
    {
        var pos = MutablePosition.FromFen("r1bqkb1r/1ppp1ppp/p1n2n2/4p3/B3P3/5N2/PPPP1PPP/RNBQK2R w KQkq - 2 5");
        pos.Move(Move.Castle(Colors.White));
        pos.CastlingRights.Should().Be(CastlingRights.BlackKing | CastlingRights.BlackQueen);

        pos.WhiteRooks.Should().Be(Bitboards.Create("A1", "F1"));
        pos.WhiteKing.Should().Be(Bitboards.Create("G1"));

        var whiteRank1 = pos.White & Bitboards.Masks.Rank_1;
        var occupiedRank1 = pos.Occupied & Bitboards.Masks.Rank_1;

        var expected = Bitboards.Create("a1", "b1", "c1", "d1", "f1", "g1");

        whiteRank1.Should().Be(expected);
        occupiedRank1.Should().Be(expected);
    }

    [Test]
    public void Black_King_Side_Castle_Should_Set_Bitboards()
    {
        var pos = MutablePosition.FromFen("r1bqk2r/1ppp1ppp/p1n2n2/4p3/Bb2P3/1P3N2/P1PP1PPP/RNBQK2R b KQkq - 0 6");
        pos.Move(Move.Castle(Colors.Black));

        pos.CastlingRights.Should().Be(CastlingRights.WhiteKing | CastlingRights.WhiteQueen);
        pos.BlackRooks.Should().Be(Bitboards.Create("A8", "F8"));
        pos.BlackKing.Should().Be(Bitboards.Create("G8"));

        var blackRank8 = pos.Black & Bitboards.Masks.Rank_8;
        var occupiedRank8 = pos.Occupied & Bitboards.Masks.Rank_8;

        var expected = Bitboards.Create("a8", "c8", "d8", "f8", "g8");

        Bitboards.Debug(occupiedRank8, expected);

        blackRank8.Should().Be(expected);
        occupiedRank8.Should().Be(expected);
    }

    [Test]
    public async Task Rook_Should_Have_Legal_After_Castling()
    {
        var pgn = """
        [White "w"]
        [Black "b"]

        1. e4 d5 2. Nf3 d4 3. Bc4 e5 *
        """;
        var (game, _) = await new PgnSerializer().ReadSingle(new StringReader(pgn));

        game.CurrentPosition.GenerateLegalMoves('K')
            .ToArray().Should().Contain(x => x.CastleIndex != 0);
        Engine.Move(game, Move.Castle(Colors.White));
        Engine.Move(game, "h7", "h6");

        game.CurrentPosition.GenerateLegalMoves('R')
            .ToArray().Should().HaveCount(1);

    }

    [Test]
    public async Task Rook_Should_Have_Legal_Moves_From_e1_After_Castle()
    {
        var pgn = """
        [White "w"]
        [Black "b"]

        1. e4 d5 2. Nf3 d4 3. Bc4 e5 4. O-O * 
        """;
        var (game, _) = await new PgnSerializer().ReadSingle(new StringReader(pgn));
        Engine.Move(game, "c8", "e6");
        Engine.Move(game, "f1", "e1");
        Engine.Move(game, "h7", "h6");

        game.CurrentPosition.GenerateLegalMoves('R')
            .ToArray().Should().NotBeEmpty();
    }
    [Test]
    public void Castling_Should_Not_Be_Allowed_When_Checked_By_Pawn()
    {
        var pos = MutablePosition.FromFen("r3k2r/p1ppqpb1/bn2pnp1/3PN3/4P3/2N2Q1p/PPpBBPPP/1K1R3R w kq - 0 1");
        var moves = pos.GenerateLegalMoves('K').ToArray();

        moves.Should().NotContain(Move.QueenSideCastle(Colors.White));
        moves.Should().NotContain(Move.Castle(Colors.White));
    }

}