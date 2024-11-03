using static System.Math;

namespace Lolbot.Core;

public sealed class Search(Game game)
{
    const int Inf = 1_000_000;   
    const int Max_Depth = 64;

    private readonly Position rootPosition = game.CurrentPosition;
    private readonly RepetitionTable history = game.RepetitionTable;

    private int searchDepth = 0;
    private int nodes = 0;
    private CancellationToken ct;
    private bool isAborted;

    public Move? BestMove()
    {
        var timer = new CancellationTokenSource(2_000);

        return BestMove(timer.Token);
    }

    public Move? BestMove(CancellationToken ct)
    {
        this.ct = ct;
        var bestMove = default(Move?);
        searchDepth = 1;

        while (bestMove is null || searchDepth <= Max_Depth && !ct.IsCancellationRequested)
        {
            var temp = SearchRoot(searchDepth, bestMove);
            if (!isAborted) bestMove = temp;

            searchDepth++;
        }

        return bestMove;
    }

    public Move SearchRoot(int depth, Move? currentBest)
    {
        nodes = 0;

        if (rootPosition.IsCheck) depth++;

        Span<Move> moves = stackalloc Move[218];
        var count = MoveGenerator.Legal(in rootPosition, ref moves);
        moves = moves[..count];

        var bestMove = currentBest ?? moves[0];

        var (alpha, beta) = (-Inf, Inf);

        int i = 0;
        for (; i < count; i++)
        {
            if (ct.IsCancellationRequested) break;

            var move = SelectMove(ref moves, currentBest, in i);
            var nextPosition = rootPosition.Move(move);

            int eval = -Inf;
            history.Update(move, nextPosition.Hash);
            // if (i == 0)
            // {
            eval = -EvaluateMove(ref nextPosition, depth - 1, 1, -beta, -alpha);
            // }
            // else
            // {
            //     eval = -EvaluateMove(ref nextPosition, depth, -alpha - 1, -alpha);
            //     if (eval > alpha && beta - alpha > 1)
            //         eval = -EvaluateMove(ref nextPosition, depth, -beta, -alpha);
            // }

            history.Unwind();

            if (eval > alpha)
            {
                alpha = eval;
                bestMove = move;
            }
        }
        nodes += i;
        Console.WriteLine($"info score cp {alpha} depth {depth} bm {bestMove} nodes {nodes}");

        return bestMove;
    }

    public int EvaluateMove(ref readonly Position position, int remainingDepth, int ply, int alpha, int beta)
    {
        if (remainingDepth == 0) return StaticEvaluation(in position);

        var mateValue = Inf - ply;
        if (alpha > mateValue) alpha = -mateValue;
        if (beta > mateValue - 1) beta = mateValue - 1;
        // if (history.IsDrawByRepetition(position.Hash)) return 0;

        Span<Move> moves = stackalloc Move[218];
        var count = MoveGenerator.Legal(in position, ref moves);
        if (count == 0) return position.IsCheck ? -mateValue : 0;
        if ((nodes & 0xf) == 0 && ct.IsCancellationRequested)
        {
            isAborted = true;
            return -Inf;
        }

        var value = -Inf;

        int i = 0;
        for (; i < count; i++)
        {
            var nextPosition = position.Move(moves[i]);
            value = Max(value, -EvaluateMove(in nextPosition, remainingDepth - 1, ply + 1, -beta, -alpha));
            alpha = Max(value, alpha);

            if (alpha >= beta) break;
        }
        nodes += i;


        return value;
    }

    public int StaticEvaluation(ref readonly Position position)
    {
        var eval = 0;

        for (Piece i = Piece.WhitePawn; i < Piece.WhiteKing; i++)
        {
            eval += Heuristics.GetPieceValue(i, position[i], position.Occupied);
        }

        for (Piece i = Piece.BlackPawn; i < Piece.BlackKing; i++)
        {
            eval -= Heuristics.GetPieceValue(i, position[i], position.Occupied);
        }

        return position.CurrentPlayer == Color.White ? eval : -eval;
    }

    private static ref readonly Move SelectMove(ref Span<Move> moves, Move? currentBest, in int i)
    {
        if (i == 0 && currentBest is not null)
        {
            var index = moves.IndexOf(currentBest.Value);
            if (index >= 0)
            {
                moves[index] = moves[0];
                moves[0] = currentBest.Value;
            }
        }

        return ref moves[i];
    }
}