using System.Diagnostics;
using Lolbot.Core;
namespace Lolbot.Tests;

public class Fuzz
{
    [Test]
    [Explicit]
    public void InconsistentBoardState()
    {
        var position = new MutablePosition();
        var previous = new Position();

        var random = new Random();
        for (ulong i = 0; i < 1; i++)
        {
            int ply = 0;
            var moves = new List<Move>(100);
            var frozenPositions = new List<Position>(100);

            while (ply < 100)
            {
                var legal = position.GenerateLegalMoves().ToArray();
                if (legal.Length == 0) break;

                var move = legal[random.Next(0, legal.Length)];

                frozenPositions.Add(previous);
                moves.Add(move);

                position.Move(in move);
                previous = previous.Move(move);

                if (position.Hash != previous.Hash)
                {
                    Console.WriteLine("Broken hash:");
                    Console.WriteLine(string.Join(',', moves));
                }

                ply++;
            }

            foreach (var (move, pos) in moves.Zip(frozenPositions).Reverse())
            {
                position.Undo(in move);
                bool areSame =
                    position.White == pos.White &&
                    position.Black == pos.Black &&
                    position.WhitePawns == pos.WhitePawns &&
                    position.WhiteKnights == pos.WhiteKnights &&
                    position.WhiteBishops == pos.WhiteBishops &&
                    position.WhiteRooks == pos.WhiteRooks &&
                    position.WhiteQueens == pos.WhiteQueens &&
                    position.WhiteKing == pos.WhiteKing &&

                    position.BlackPawns == pos.BlackPawns &&
                    position.BlackKnights == pos.BlackKnights &&
                    position.BlackBishops == pos.BlackBishops &&
                    position.BlackRooks == pos.BlackRooks &&
                    position.BlackQueens == pos.BlackQueens &&
                    position.BlackKing == pos.BlackKing &&

                    position.Occupied == pos.Occupied &&
                    position.Empty == pos.Empty &&
                    position.CastlingRights == pos.CastlingRights &&
                    position.EnPassant == pos.EnPassant;

                if (!areSame)
                {
                    Console.WriteLine($"Undoing move {move} Invalid state");
                }


            }
        }
    }
}