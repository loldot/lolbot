using Lolbot.Core;

namespace Lolbot.Tests;

public class MovePick
{
    private int[] empty_history = new int[4096];
    private Move[] empty_killers = new Move[64];

    [Test]
    public void Should_Select_Move_From_TT_First()
    {
        var ttMove = new Move('P', "e2", "e4");
        var pos = new MutablePosition();
        Span<Move> moves = stackalloc Move[256];

        var movePick = new MovePicker(
            ref empty_killers,
            ref empty_history,
            ref moves,
            pos, ttMove, 0);

        var pick = movePick.SelectMove(0);

        pick.Should().Be(ttMove);
        movePick.Count.Should().Be(0);
    }

    [Test]
    public void Should_Generate_Moves_On_Demand()
    {
        var ttMove = new Move('P', "e2", "e4");
        var pos = new MutablePosition();
        Span<Move> moves = stackalloc Move[256];

        var movePick = new MovePicker(
            ref empty_killers,
            ref empty_history,
            ref moves,
            pos, ttMove, 0);

        var pick = movePick.SelectMove(1);

        movePick.Count.Should().Be(20);
    }

    [Test]
    public void Should_Pick_History_Move_First()
    {
        var historyMove = new Move('N', "g1", "f3");
        var pos = new MutablePosition();

        Span<Move> moves = stackalloc Move[256];
        var history = new int[4096];
        history[historyMove.value & 0xfffu] = 64;

        var movePick = new MovePicker(
            ref empty_killers,
            ref history,
            ref moves,
            pos, Move.Null, 0);

        var pick = movePick.SelectMove(0);
        pick.Should().Be(historyMove);
        movePick.Count.Should().Be(20);
    }

}