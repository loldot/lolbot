using System.Runtime.CompilerServices;
using System.Diagnostics;
using static System.Math;

namespace Lolbot.Core;

public static class Engine
{
    const int Inf = 999_999;
    const int Mate = ushort.MaxValue;

    private static readonly TranspositionTable tt = new TranspositionTable();
    private static readonly int[] historyHeuristic = new int[4096];
    private static int nodes = 0;

    public static void Init()
    {
        Console.WriteLine("info " + MovePatterns.PextTable.Length);
    }

    public static Game NewGame() => new Game(new Position());

    public static Game FromPosition(string fenstring)
    {
        return new Game(Position.FromFen(fenstring));
    }

    public static void Move(Game game, string from, string to)
    {
        Move(
            game,
            Squares.FromCoordinates(from),
            Squares.FromCoordinates(to)
        );
    }

    public static void Move(Game game, Square from, Square to)
    {
        var move = game.GenerateLegalMoves()
            .FirstOrDefault(x => x.FromIndex == Squares.ToIndex(from) && x.ToIndex == Squares.ToIndex(to));
        Move(game, move);
    }

    public static void Move(Game game, Move move)
    {
        if (!game.IsLegalMove(move)) throw new ArgumentException("Invalid move");
        game.Move(in move);
    }

    public static int Evaluate(MutablePosition position)
    {
        var eval = 0;
        int color = position.CurrentPlayer == Colors.White ? 1 : -1;

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

    public static int PerftDiff(in Position position, MutablePosition position2, int remainingDepth = 4, int split = 0)
    {
        Span<Move> moves1 = stackalloc Move[218];
        Span<Move> moves2 = stackalloc Move[218];

        var currentCount = MoveGenerator.Legal(in position, ref moves1);
        var currentCount2 = MoveGenerator2.Legal(position2, ref moves2);

        var count = 0;
        if (currentCount != currentCount2)
        {
            var diff1 = moves1.ToArray().Except(moves2.ToArray());
            var diff2 = moves2.ToArray().Except(moves1.ToArray());

            Console.WriteLine("Err: pos1");
            Console.WriteLine(position);
            Console.WriteLine(position.EnPassant);
            Console.WriteLine(position.CastlingRights);
            Console.WriteLine(position.IsCheck);
            Console.WriteLine(position.IsPinned);

            Console.WriteLine();
            Console.WriteLine("Err: pos2");
            Console.WriteLine();
            Console.WriteLine(position2);
            Console.WriteLine(position2.EnPassant);
            Console.WriteLine(position2.CastlingRights);
            Console.WriteLine(position2.IsCheck);
            Console.WriteLine(position2.IsPinned);

            foreach (var m in diff1)
            {
                Console.WriteLine(m);
            }
            Console.WriteLine();
            foreach (var m in diff2)
            {
                Console.WriteLine(m);
            }

            throw new Exception("err in pos");
        }

        if (remainingDepth == 1) return currentCount2;

        for (int i = 0; i < currentCount; i++)
        {
            position2.Move(in moves1[i]);
            var posCount = PerftDiff(position.Move(moves1[i]), position2, remainingDepth - 1);
            position2.Undo(in moves1[i]);

            // if (position.Hash != position2.Hash)
            // {
            //     Console.WriteLine("Err: pos1");
            //     Console.WriteLine(position);
            //     Console.WriteLine(position.EnPassant);
            //     Console.WriteLine(position.CastlingRights);
            //     Console.WriteLine(position.IsCheck);
            //     Console.WriteLine(position.IsPinned);

            //     Console.WriteLine();
            //     Console.WriteLine("Err: pos2");
            //     Console.WriteLine();
            //     Console.WriteLine(position2);
            //     Console.WriteLine(position2.EnPassant);
            //     Console.WriteLine(position2.CastlingRights);
            //     Console.WriteLine(position2.IsCheck);
            //     Console.WriteLine(position2.IsPinned);
            // }
            count += posCount;
        }
        return count;
    }
    public static int Perft2(MutablePosition position, int remainingDepth = 4, int split = 0)
    {
        Span<Move> moves = stackalloc Move[218];
        var currentCount = MoveGenerator2.Legal(position, ref moves);
        var count = 0;

        if (remainingDepth == 1) return currentCount;

        for (int i = 0; i < currentCount; i++)
        {
            position.Move(ref moves[i]);

            var posCount = Perft2(position, remainingDepth - 1);
            position.Undo(ref moves[i]);
            if (remainingDepth == split) Console.WriteLine($"{moves[i]}: {posCount}");
            count += posCount;
        }
        return count;
    }

    public static Move? BestMove(Game game)
    {
        var timer = new CancellationTokenSource(2_000);
        return BestMove(game, timer.Token);
    }

    public static Move? BestMove(Game game, CancellationToken ct)
    {
        // Age history heuristic
        for (int i = 0; i < historyHeuristic.Length; i++)
            historyHeuristic[i] /= 8;

        var search = new Search(game, tt, historyHeuristic);
        return search.BestMove(ct);
    }

    internal static void Reset()
    {
        // tt.Clear();
        Array.Clear(historyHeuristic);
    }
}