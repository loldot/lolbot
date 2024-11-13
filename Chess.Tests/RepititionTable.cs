using Lolbot.Core;

namespace Lolbot.Tests;

public class RepetitionTables
{
    [Test]
    public void Should_Return_Draw()
    {
        var table = new RepetitionTable();
        table.Update(new Move('N', "e1", "d2"), 1);
        table.Update(new Move('n', "e1", "e2"), 2);
        table.Update(new Move('N', "d2", "e1"), 1);

        table.IsDrawByRepetition(1).Should().BeTrue();
    }

    [Test]
    public void Should_Reset_Draw_When_Unwinding()
    {
        var table = new RepetitionTable();
        table.Update(new Move('N', "e1", "d2"), 1);
        table.Update(new Move('n', "e1", "e2"), 2);
        table.Update(new Move('N', "d2", "e1"), 1);
        table.Unwind();

        table.IsDrawByRepetition(1).Should().BeFalse();
    }

    [Test]
    public async ValueTask Should_Find_Draw()
    {
        var pgn = new PgnSerializer();
        var (game, _) = await pgn.Read(File.OpenRead("./TestData/Berlin-Draw.pgn"));

        game.CurrentPosition.Move(new Move(Piece.BlackQueen, Squares.E6, Squares.D6));
        game.RepetitionTable.IsDrawByRepetition(game.CurrentPosition.Hash).Should().BeTrue();
    }
}