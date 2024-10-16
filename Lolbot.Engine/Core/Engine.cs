using System.Runtime.CompilerServices;
using static System.Math;

namespace Lolbot.Core;

public static class Engine
{
    private static readonly TranspositionTable tt = new TranspositionTable();


    public static void Init()
    {
        Console.WriteLine("info " + MovePatterns.PextTable.Length);
    }

    public static Game NewGame() => new Game();

    public static Game FromPosition(string fenstring)
    {
        return new Game(Position.FromFen(fenstring), []);
    }

    public static Game Move(Game game, string from, string to)
    {
        return Move(
            game,
            Squares.FromCoordinates(from),
            Squares.FromCoordinates(to)
        );
    }

    public static Game Move(Game game, Square from, Square to)
    {
        var move = game.CurrentPosition
            .GenerateLegalMoves()
            .ToArray()
            .FirstOrDefault(x => x.FromIndex == Squares.ToIndex(from) && x.ToIndex == Squares.ToIndex(to));
        return Move(game, move);
    }

    public static Game Move(Game game, Move move)
    {
        if (!game.IsLegalMove(move)) throw new ArgumentException("Invalid move");

        return new Game(game.InitialPosition, [.. game.Moves, move]);
    }


    public static int Evaluate(Position position)
    {
        var eval = 0;
        int color = position.CurrentPlayer == Color.White ? 1 : -1;

        if (position.IsCheck)
        {
            eval -= color * 50;
        }

        eval += Heuristics.Mobility(position, Color.White);
        eval -= Heuristics.Mobility(position, Color.Black);

        eval += Heuristics.IsolatedPawns(position, Color.White);
        eval -= Heuristics.IsolatedPawns(position, Color.Black);

        for (Piece i = Piece.WhitePawn; i < Piece.WhiteKing; i++)
        {
            eval += Heuristics.GetPieceValue(i, position[i], position.Occupied);
        }
        for (Piece i = Piece.BlackPawn; i < Piece.BlackKing; i++)
        {
            eval -= Heuristics.GetPieceValue(i, position[i], position.Occupied);
        }
        return color * eval;
    }

    public static int Perft(in Position position, int remainingDepth = 4, int split = 0)
    {
        Span<Move> moves = stackalloc Move[218];
        var currentCount = MoveGenerator.Legal(in position, ref moves);
        var count = 0;

        if (remainingDepth == 1) return currentCount;

        for (int i = 0; i < currentCount; i++)
        {
            var posCount = Perft(position.Move(moves[i]), remainingDepth - 1);
            if (remainingDepth == split) Console.WriteLine($"{moves[i]}: {posCount}");
            count += posCount;
        }
        return count;
    }

    public static Move? BestMove(Game game)
    {
        return BestMove(game, 2_000);
    }

    public static Move? BestMove(Game game, int timoutMs)
    {
        var search = new Search(tt);
        return search.BestMove(game, timoutMs);
    }
}