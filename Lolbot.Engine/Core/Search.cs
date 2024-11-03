using static System.Math;

namespace Lolbot.Core;

public sealed class Search(Game game)
{
    const int Inf = 1_000_000;
    const int Mate = -999_999;
    const int Max_Depth = 3;

    private readonly Position rootPosition = game.CurrentPosition;
    private readonly RepetitionTable history = game.RepetitionTable;

    private int nodes = 0;

    public Move? BestMove()
    {
        var timer = new CancellationTokenSource(2_000);

        return BestMove(Max_Depth, null, timer.Token);
    }

    public Move BestMove(int depth, Move? currentBest, CancellationToken ct)
    {
        nodes = 0;

        if (rootPosition.IsCheck) depth++;

        Span<Move> moves = stackalloc Move[218];
        var count = MoveGenerator.Legal(in rootPosition, ref moves);
        moves = moves[..count];

        var bestEval = -Inf;
        var bestMove = currentBest ?? moves[0];

        var (alpha, beta) = (-Inf, Inf);
        int i = 0;
        for (; i < count; i++)
        {
            if (ct.IsCancellationRequested) break;

            var move = moves[i]; //SelectMove(ref moves, ref currentBest, ref i);
            var nextPosition = rootPosition.Move(move);

            int eval;
            history.Update(move, nextPosition.Hash);
            // if (i == 0)
            // {
            eval = -EvaluateMove(ref nextPosition, depth - 1, -beta, -alpha);
            // }
            // else
            // {
            //     eval = -EvaluateMove(ref nextPosition, depth, -alpha - 1, -alpha);
            //     if (eval > alpha && beta - alpha > 1)
            //         eval = -EvaluateMove(ref nextPosition, depth, -beta, -alpha);
            // }

            history.Unwind();

            if (eval > bestEval)
            {
                bestEval = eval;
                bestMove = move;
                alpha = Max(alpha, eval);
            }
        }
        nodes += i;
        Console.WriteLine($"info score cp {bestEval} depth {depth} bm {bestMove} nodes {nodes}");

        return bestMove;
    }

    public int EvaluateMove(ref readonly Position position, int depth, int alpha, int beta)
    {
        // if depth = 0 or node is a terminal node then
        //         return color × the heuristic value of node
        //     value := −∞
        //     for each child of node do
        //         value := max(value, −negamax(child, depth − 1, −color))
        //     return value

        if (depth == 0) return StaticEvaluation(in position);

        Span<Move> moves = stackalloc Move[218];
        var count = MoveGenerator.Legal(in position, ref moves);
        if (count == 0) return position.IsCheck ? (Mate - depth) : 0;

        var value = -Inf;
        int i = 0;
        for (; i < count; i++)
        {
            var nextPosition = position.Move(moves[i]);
            value = Max(value, -EvaluateMove(in position, depth - 1, -beta, -alpha));
        }
        nodes += i;

        return value;
    }

    public static int StaticEvaluation(ref readonly Position position)
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
}