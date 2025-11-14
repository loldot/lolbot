using Lolbot.Core;

namespace Lolbot.Tests;

[Category(TestSuites.Fast)]
public class MovePickerOrdering
{
    [Test]
    public void Killer_Move_Should_Be_Scored_Above_History()
    {
        var pos = MutablePosition.FromFen("rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1");
        Span<Move> buffer = stackalloc Move[256];

        var killers = new Move[64];
        int[][] history = [new int[4096], new int[4096]];

        // pick two legal quiet moves, set one as killer, give another huge history to compete
        MovePicker mp = new MovePicker(ref killers, ref history, ref buffer, pos, Move.Null, ply: 0);
        var first = mp.SelectMove(1); // generate
        var second = mp.SelectMove(2);

        killers[0] = first;
        history[second.Color][second.value & 0xfff] = 10_000; // large but should lose to killer bonus

        // re-create to force regeneration with scores
        mp = new MovePicker(ref killers, ref history, ref buffer, pos, Move.Null, ply: 0);
        var top = mp.SelectMove(0);

        top.Should().Be(first);
    }

    [Test]
    public void PickCapture_Should_Return_MVV_LVA_Descending()
    {
        // Setup: simple position with several captures available
        var pos = MutablePosition.FromFen("r3k2r/ppp2ppp/2n5/3pp3/3PP3/2N5/PPP2PPP/R3K2R w KQkq - 0 1");
        Span<Move> buffer = stackalloc Move[256];
        var killers = new Move[64];
        int[][] history = [new int[4096], new int[4096]];

        MovePicker mp = new MovePicker(ref killers, ref history, ref buffer, pos, Move.Null, ply: 0);

        var m0 = mp.PickCapture(0);
        var m1 = mp.PickCapture(1);

        if (m1.IsNull) Assert.Pass("Only one capture available in this setup");

        var s0 = Heuristics.MVV_LVA(m0.CapturePieceType, m0.FromPieceType) + Heuristics.GetPieceValue(m0.PromotionPiece);
        var s1 = Heuristics.MVV_LVA(m1.CapturePieceType, m1.FromPieceType) + Heuristics.GetPieceValue(m1.PromotionPiece);

        s0.Should().BeGreaterThanOrEqualTo(s1);
    }

    // [Test]
    // public async Task PickEvasion_Should_Yield_TT_Move_First()
    // {
    //     var pgn = """
    //     [White ]
    //     1. e4 d5 2. Bb5+ *
    //     """;
    //     var (game, _) = await new PgnSerializer().ReadSingle(new StringReader(pgn));
    //     var pos = game.CurrentPosition;

    //     Span<Move> buffer = stackalloc Move[256];
    //     var killers = new Move[64];
        
    //     int[][] history = [new int[4096], new int[4096]];

    //     // pick any legal evasion as tt move
    //     var evasions = pos.GenerateLegalMoves().ToArray();
    //     evasions.Should().NotBeEmpty();
    //     var tt = evasions[0];

    //     MovePicker mp = new MovePicker(ref killers, ref history, ref buffer, pos, tt, ply: 0);
    //     var first = mp.PickEvasion(0);
    //     first.Should().Be(tt);
    // }
}
