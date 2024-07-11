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
}