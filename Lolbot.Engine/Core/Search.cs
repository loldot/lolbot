using System.Runtime.CompilerServices;
using static System.Math;

namespace Lolbot.Core;

public class Search
{
    const int Max_Depth = 64;

    private bool isValid;
    private int nodes = 0;
    private readonly int[] pvLengths = new int[Max_Depth];
    private readonly Move[][] pv = new Move[Max_Depth][];

    private readonly int[] historyHeuristic = new int[4096];
    private readonly TranspositionTable tt;
    private readonly CancellationToken ct;

    public Search(TranspositionTable tt, CancellationToken ct)
    {
        this.tt = tt;
        this.ct = ct;

        for (int i = 0; i < Max_Depth; i++)
        {
            // Account for extensions
            pv[i] = new Move[2 * Max_Depth];
            pvLengths[i] = i;
        }
    }

    public Move? BestMove(Game game, CancellationToken ct)
    {
        // var hash = game.CurrentPosition.Hash;
        // var pv = tt.Get(hash);
        Move? bestMove = null;// = pv.IsSet && pv.Key == hash && pv.Type == TranspositionTable.Exact ? pv.BestMove : null;
        var depth = 1;

        while (depth <= Max_Depth && !ct.IsCancellationRequested)
        {
            (_, bestMove) = BestMove(game, depth);
            depth++;
        }

        return bestMove;
    }


    public (int eval, Move? bestMove) BestMove(Game game, int depth)
    {
        Span<Move> moves = stackalloc Move[218];

        var history = game.RepetitionTable;
        var position = game.CurrentPosition;

        nodes = 0;
        isValid = true;
        var bestMove = pv[0][0];
        var bestEval = Pvs<Pv>(history, ref position, 0, depth, -999_999, 999_999, 1);

        if (isValid)
        {
            bestMove = pv[0][0];
            var pvLine = string.Join(' ', pv[0][0..pvLengths[0]]);
            Console.WriteLine($"info score cp {bestEval} depth {depth} bm {bestMove} nodes {nodes} pv {pvLine}");
        }

        // for (int i = 0; i < depth; i++)
        //     Console.WriteLine( string.Join(' ', pv[i][0..pvLengths[i]]));

        return (bestEval, bestMove);
    }

    public interface NodeType { };

    public struct Pv : NodeType { }
    public struct NonPv : NodeType { }

    public int Pvs<TNode>(RepetitionTable history, ref Position position, int ply, int depth, int alpha, int beta, int color) where TNode : NodeType
    {
        var eval = -999_999;
        var alphaOrig = alpha;

        if (history.IsDrawByRepetition(position.Hash)) return 0;
        if (tt.TryGet(position.Hash, depth, out var ttEntry))
        {
            if (ttEntry.Type == TranspositionTable.Exact)
                return ttEntry.Evaluation;
            else if (ttEntry.Type == TranspositionTable.LowerBound)
                alpha = Max(alpha, ttEntry.Evaluation);
            else if (ttEntry.Type == TranspositionTable.UpperBound)
                beta = Min(beta, ttEntry.Evaluation);

            if (alpha >= beta)
                return ttEntry.Evaluation;
        }
        else if (depth > 3) depth--;

        Span<Move> moves = stackalloc Move[218];
        var count = MoveGenerator.Legal(ref position, ref moves);

        if (count == 0) return position.IsCheck ? (ply - 999_999) : 0;
        if (position.IsCheck) depth++;
        if (depth == 0) return QuiesenceSearch(position, alpha, beta, color);

        moves = moves[..count];
        OrderMoves(ref moves, ply);

        Move bestMove = moves[0];
        for (byte i = 0; i < count; i++)
        {

            var nextPosition = position.Move(moves[i]);
            history.Update(moves[i], nextPosition.Hash);

            if (i == 0)
            {
                eval = -Pvs<TNode>(history, ref nextPosition, ply + 1, depth - 1, -beta, -alpha, -color);
            }
            else
            {
                eval = -Pvs<NonPv>(history, ref nextPosition, ply + 1, depth - 1, -alpha - 1, -alpha, -color);
                if (typeof(TNode) == typeof(Pv) && eval > alpha)
                    eval = -Pvs<Pv>(history, ref nextPosition, ply + 1, depth - 1, -beta, -alpha, -color);
            }
            nodes++;

            history.Unwind();

            if (eval > alpha)
            {
                alpha = eval;
                pv[ply][ply] = bestMove = moves[i];
                for (int nextPly = ply + 1; nextPly < pvLengths[ply + 1]; nextPly++)
                {
                    pv[ply][nextPly] = pv[ply + 1][nextPly];
                }
                pvLengths[ply] = pvLengths[ply + 1];
            }
            if (eval >= beta) return eval;
            if (alpha >= beta)
            {
                historyHeuristic[64 * moves[i].FromIndex + moves[i].ToIndex] = depth * depth;
                break;
            }
            // if (ct.IsCancellationRequested) return alpha;
        }

        byte ttType;
        if (eval <= alphaOrig) ttType = TranspositionTable.UpperBound;
        else if (eval >= beta) ttType = TranspositionTable.LowerBound;
        else ttType = TranspositionTable.Exact;

        tt.Add(position.Hash, depth, eval, ttType, bestMove);

        return alpha;
    }

    private int QuiesenceSearch(in Position position, int alpha, int beta, int color)
    {
        Span<Move> moves = stackalloc Move[218];

        var standPat = Engine.Evaluate(position);

        if (standPat >= beta) return beta;
        if (alpha < standPat) alpha = standPat;

        var count = MoveGenerator.Captures(in position, ref moves);
        moves = moves[..count];
        moves.Sort(MoveComparer);

        for (byte i = 0; i < count; i++)
        {
            var eval = -QuiesenceSearch(position.Move(moves[i]), -beta, -alpha, -color);
            nodes++;

            if (eval >= beta) return beta;

            alpha = Max(eval, alpha);
        }

        return alpha;
    }

    private void OrderMoves(ref Span<Move> legalMoves, int ply)
    {
        var offset = 0;

        if (ply < pvLengths[ply])
        {
            var pvMove = pv[ply][ply];
            var index = legalMoves.IndexOf(pvMove);
            if (index >= 0)
            {
                legalMoves[index] = legalMoves[0];
                legalMoves[0] = pvMove;
                offset = 1;
            }
        }

        legalMoves[offset..].Sort(MoveComparer);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int MoveComparer(Move x, Move y)
    {
        int score = 0;

        score -= 1000 * Heuristics.GetPieceValue(x.PromotionPiece);
        score -= 1000 * Heuristics.MVV_LVA(x.CapturePiece, x.FromPiece);
        score -= historyHeuristic[64 * x.FromIndex + x.ToIndex];

        score += 1000 * Heuristics.GetPieceValue(y.PromotionPiece);
        score += 1000 * Heuristics.MVV_LVA(y.CapturePiece, y.FromPiece);
        score += historyHeuristic[64 * y.FromIndex + y.ToIndex];

        return score;
    }
}