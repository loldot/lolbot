using Chess.Api;

namespace Chess.Tests;

public class Moves
{
    [Test]
    public void GeneratePawnAttaks()
    {
        var pawnstructure = Utils.FromArray([
            0,0,0,0,0,0,0,0,
            0,0,0,0,0,0,0,0,
            0,0,0,0,0,0,0,0,
            0,0,0,0,0,0,0,0,
            0,0,1,0,0,0,0,0,
            1,1,0,1,0,1,1,1,
            0,0,0,0,0,0,0,0
        ]);

        var pos = new Position() with {
            WhitePawns = pawnstructure
        };

        pos.WhitePawnAttacks().Should().Be(Utils.FromArray([
            0,0,0,0,0,0,0,0,
            0,0,0,0,0,0,0,0,
            0,0,0,0,0,0,0,0,
            0,1,0,1,0,0,0,0,
            1,1,1,0,1,1,1,1,
            0,0,0,0,0,0,0,0,
            0,0,0,0,0,0,0,0
        ]));
        
    }

    [TestCase("A2", "A4", 8, 24)]
    [TestCase("E5", "D4", 36, 27)]
    [TestCase("H8", "A1", 63, 0)]
    [TestCase("B1", "H4", 1, 31)]
    public void CheckMoveType(string from, string to, byte fromIdx, byte toIdx)
    {
        var move = new Move(
            Utils.SquareFromCoordinates(from),
            Utils.SquareFromCoordinates(to)
        );

        move.FromIndex.Should().Be(fromIdx);
        move.ToIndex.Should().Be(toIdx);
    }
}