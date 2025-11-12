using System.Runtime.CompilerServices;
using System.Diagnostics;
using static System.Math;

namespace Lolbot.Core;

public static class Engine
{
    const int Inf = 999_999;
    const int Mate = ushort.MaxValue;

    private static readonly TranspositionTable tt = new TranspositionTable();
    private static readonly int[][] historyHeuristic = [
        new int[4096], new int[4096]
    ];

    public static void Init()
    {
        Console.WriteLine("info " + MovePatterns.PextTable.Length);
    }

    public static Game NewGame() => new Game(new MutablePosition());

    public static Game FromPosition(string fenstring)
    {
        return new Game(MutablePosition.FromFen(fenstring));
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

    public static void Move(Game game, string from, string to, char promotionPiece)
    {
        Move(
            game,
            Squares.FromCoordinates(from),
            Squares.FromCoordinates(to),
            promotionPiece
        );
    }
    public static void Move(Game game, Square from, Square to, char promotionPiece)
    {
        var move = game.GenerateLegalMoves()
            .FirstOrDefault(x =>
                x.FromIndex == Squares.ToIndex(from)
            && x.ToIndex == Squares.ToIndex(to)
            && x.PromotionPieceType == Utils.GetPieceType(promotionPiece));

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

    public static int Perft2(MutablePosition position, int remainingDepth = 4, int split = 0)
    {
        Span<Move> moves = stackalloc Move[218];
        var currentCount = MoveGenerator.Legal(position, ref moves);
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

    public static Move? BestMove(Game game, int depth)
    {
        // Age history heuristic
        AgeHistory();

        var search = new Search(game, tt, historyHeuristic)
        {
            OnSearchProgress = Uci.PrintProgress
        };
        return search.BestMove(depth);
    }



    public static Move? BestMove(Game game, CancellationToken ct)
    {
        // Age history heuristic
        AgeHistory();

        var search = new Search(game, tt, historyHeuristic)
        {
            OnSearchProgress = Uci.PrintProgress
        };
        return search.BestMove(ct);
    }

    internal static void Reset()
    {
        // tt.Clear();
        for (int i = 0; i < historyHeuristic.Length; i++)
        {
            Array.Clear(historyHeuristic[i]);
        }
    }

    private static void AgeHistory()
    {
        for (int i = 0; i < historyHeuristic.Length; i++)
        {
            for (int j = 0; j < historyHeuristic[i].Length; j++)
            {
                historyHeuristic[i][j] /= 8;
            }
        }
    }
}