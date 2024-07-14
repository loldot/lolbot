using Lolbot.Core;
namespace Lolbot.Tests;

public class Fuzz
{
    [Test]
    [Explicit]
    public void InconsistentBoardState()
    {
        var game = new Game();

        var random = new Random();
        for (ulong i = 0; i < 100_000; i++)
        {
            while (game.PlyCount <= 100)
            {
                var legal = game.CurrentPosition.GenerateLegalMoves().ToArray();
                if (legal.Length == 0) break;

                game = Engine.Move(game, legal[random.Next(0, legal.Length)]);
                var position = game.CurrentPosition;

                if ((position.White | position.Black) != position.Occupied)
                {
                    Console.WriteLine("Inconsistent position b+w != occ");
                    Console.WriteLine(new FenSerializer().ToFenString(position));
                    Console.WriteLine(string.Join(',', game.Moves));
                }

                if ((position.WhitePawns | position.WhiteRooks | position.WhiteBishops
                    | position.WhiteKnights | position.WhiteQueens | position.WhiteKing) != position.White)
                {
                    Console.WriteLine("Inconsistent position pices != white");
                    Console.WriteLine(new FenSerializer().ToFenString(position));
                    Console.WriteLine(string.Join(',', game.Moves));
                }

                if ((position.BlackPawns | position.BlackRooks | position.BlackBishops
                    | position.BlackKnights | position.BlackQueens | position.BlackKing) != position.Black)
                {
                    Console.WriteLine("Inconsistent position pices != black");
                    Console.WriteLine(new FenSerializer().ToFenString(position));
                    Console.WriteLine(string.Join(',', game.Moves));
                }
            }
        }
    }
}