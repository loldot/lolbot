using Lolbot.Core;

namespace Lolbot.Tests;

[Category(TestSuites.Fast)]
public class MVV_LVA
{
    [Test]
    public void RxR_Should_Be_Before_BxP()
    {
        var rxr = Heuristics.MVV_LVA(Piece.BlackRook, Piece.WhiteRook);
        var bxp = Heuristics.MVV_LVA(Piece.BlackPawn, Piece.WhiteBishop);

        rxr.Should().BeGreaterThan(bxp);
    }

    [Test]
    public void PxQ_Should_Be_Before_QxP()
    {
        var pxq = Heuristics.MVV_LVA(Piece.BlackQueen, Piece.WhitePawn);
        var qxp = Heuristics.MVV_LVA(Piece.BlackPawn, Piece.WhiteQueen);

        pxq.Should().BeGreaterThan(qxp);
    }

    [Test]
    public void NxQ_Should_Be_Before_RxQ()
    {
        var nxq = Heuristics.MVV_LVA(Piece.BlackQueen, Piece.WhiteKnight);
        var rxq = Heuristics.MVV_LVA(Piece.BlackQueen, Piece.WhiteRook);

        nxq.Should().BeGreaterThan(rxq);
    }

    [Test]
    public void BxB_Should_Be_Before_NxN()
    {
        var bxb = Heuristics.MVV_LVA(Piece.BlackBishop, Piece.WhiteBishop);
        var nxn = Heuristics.MVV_LVA(Piece.BlackKnight, Piece.WhiteKnight);

        bxb.Should().BeGreaterThan(nxn);
    }
}