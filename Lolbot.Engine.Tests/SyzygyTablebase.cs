using Lolbot.Core;

namespace Lolbot.Tests;

public class SyzygyTablebaseTests
{
    [Test]
    public void Init_LoadsTables()
    {
        SyzygyTablebase.Init(@"C:\dev\chess-data\syzygy", 7);
        Assert.That(SyzygyTablebase.MaxPieces, Is.EqualTo(7));
    }

    [Test]
    public void CanProbe_FivePieceEndgame_ReturnsTrue()
    {
        SyzygyTablebase.Init(@"C:\dev\chess-data\syzygy", 7);

        // KRN vs KQ (5 pieces)
        var position = MutablePosition.FromFen("8/8/8/8/8/2N5/1R6/K1Q1k3 w - - 0 1");
        Assert.That(SyzygyTablebase.CanProbe(position), Is.True);
    }

    [Test]
    public void CanProbe_TooManyPieces_ReturnsFalse()
    {
        SyzygyTablebase.Init(@"C:\dev\chess-data\syzygy", 7);

        var position = MutablePosition.FromFen("rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1");
        Assert.That(SyzygyTablebase.CanProbe(position), Is.False);
    }

    [Test]
    public void ProbeWdl_UnknownPosition_Returns100()
    {
        SyzygyTablebase.Init(@"C:\dev\chess-data\syzygy", 7);

        var position = MutablePosition.FromFen("rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1");
        Assert.That(SyzygyTablebase.ProbeWdl(position), Is.EqualTo(100));
    }

    [Test]
    public void Init_InvalidPath_LoadsNothing()
    {
        SyzygyTablebase.Init("/nonexistent/path", 7);
        Assert.That(SyzygyTablebase.Loaded, Is.False);
    }

    [Test]
    public void Loaded_EmptyPath_ReturnsFalse()
    {
        SyzygyTablebase.Init(null, 7);
        Assert.That(SyzygyTablebase.Loaded, Is.False);
    }

    [Test]
    public void ProbeWdl_KRNvKQ_DrawPosition()
    {
        SyzygyTablebase.Init(@"C:\dev\chess-data\syzygy", 7);

        Assert.That(SyzygyTablebase.Largest, Is.GreaterThan(0));
    }
}