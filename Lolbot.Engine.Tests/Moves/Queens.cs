using Lolbot.Core;

namespace Lolbot.Tests;

[Category(TestSuites.Fast)]
public class Queens
{
    [Test]
    public void QueensFromCenter()
    {
        var bishops = Bitboards.Create("C4", "D4");
        var blockers = Bitboards.Create("b3", "b4", "b5", "c7", "d3", "d6", "f2", "f7");

        var quenMoves = MovePatterns.GenerateBishopAttacks(bishops, ~blockers)
        | MovePatterns.GenerateRookAttacks(bishops, ~blockers);

        var expected = Bitboards.Create((int[])[
            0,0,0,0,0,0,0,1,//8
            1,0,1,0,0,1,1,0,//7
            0,1,1,1,1,1,0,0,//6
            0,1,1,1,1,0,0,0,//5
            0,1,1,1,1,1,1,1,//4
            0,1,1,1,1,0,0,0,//3
            0,1,1,0,0,1,0,0,//2
            1,0,1,0,0,0,0,0 //1
        /** A B C D E F G H **/
        ]);

        quenMoves.Should().Be(expected);
    }
}