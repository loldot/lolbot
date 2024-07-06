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

        Console.WriteLine(string.Join(',', moves));
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
}