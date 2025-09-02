using Lolbot.Core;

namespace Lolbot.Tests;

[Category(TestSuites.Fast)]
public class FenRoundtripAndStalemate
{
    [Test]
    public void Fen_Roundtrip_Should_Preserve_State()
    {
        var pos = MutablePosition.FromFen("r3k2r/ppp2ppp/2n5/3pp3/3PP3/2N5/PPP2PPP/R3K2R w KQkq - 0 1");
        var fen = FenSerializer.ToFenString(pos);
        var back = FenSerializer.Parse(fen);

        back.White.Should().Be(pos.White);
        back.Black.Should().Be(pos.Black);
        back.CastlingRights.Should().Be(pos.CastlingRights);
        back.EnPassant.Should().Be(pos.EnPassant);
        back.CurrentPlayer.Should().Be(pos.CurrentPlayer);
        back.Hash.Should().Be(Hashes.New(back));
    }

    [Test]
    public void Stalemate_Position_Should_Have_No_Legal_Moves()
    {
        // Classic stalemate: Black to move, no legal moves, not in check
        var pos = MutablePosition.FromFen("7k/5Q2/6Q1/8/8/8/8/7K b - - 0 1");
        pos.IsCheck.Should().BeFalse();
        pos.GenerateLegalMoves().ToArray().Should().BeEmpty();
    }
}
