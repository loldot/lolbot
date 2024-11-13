// using Lolbot.Core;
// namespace Lolbot.Tests;

// public class Fuzz
// {
//     [Test]
//     [Explicit]
//     public void InconsistentBoardState()
//     {
//         var game = new Game();

//         var random = new Random();
//         for (ulong i = 0; i < 100_000; i++)
//         {
//             while (game.PlyCount <= 100)
//             {
//                 var legal = game.CurrentPosition.GenerateLegalMoves().ToArray();
//                 if (legal.Length == 0) break;

//                 game = Engine.Move(game, legal[random.Next(0, legal.Length)]);
//                 var position = game.CurrentPosition;

//                 if (position.Hash != Hashes.New(position))
//                 {
//                     Console.WriteLine("Broken hash:");
//                     Console.WriteLine(new FenSerializer().ToFenString(position));
//                     Console.WriteLine(string.Join(',', game.Moves));
//                 }
//             }
//         }
//     }
// }