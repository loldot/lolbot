using Lolbot.Core;

namespace Lolbot.Tests;

[Category(TestSuites.Full)]
public class HashFuzz
{
    [Test]
    public void FuzzHash()
    {
        Stack<Move> testMoves = new();
        Span<Move> moves = stackalloc Move[216];

        var position = new MutablePosition();

        for (int i = 0; i < 10000; i++)
        {
            if (position.plyfromRoot > 100)
            {
                position = new MutablePosition();
                testMoves.Clear();
            }

            var moveCount = MoveGenerator.Legal(position, ref moves);
            var random = Random.Shared.Next(moveCount + 1);
            if (random >= moveCount)
            {
                if (testMoves.Count == 0) continue;

                Move m = testMoves.Pop();
                position.Undo(ref m);
            }
            else
            {
                Move m = moves[random];
                position.Move(ref m);
                testMoves.Push(m);

                if (position.Hash != Hashes.New(position))
                {
                    Console.WriteLine(string.Join(" ", testMoves.ToArray()));
                    Console.WriteLine(position.ToDebugString());
                    Console.WriteLine($"{Hashes.New(position):x}");
                    Console.WriteLine($"{position.Hash:x}");
                    Console.WriteLine($"FEN: {FenSerializer.ToFenString(position)}");

                    throw new Exception("Hash mismatch");
                }
            }
        }
        Assert.Pass();
    }
}