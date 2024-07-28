using System.Collections.Immutable;
using static System.Math;

namespace Lolbot.Core;

[Flags]
public enum Castle : byte
{
    None = 0,
    WhiteQueen = 1,
    WhiteKing = 2,
    BlackQueen = 4,
    BlackKing = 8,
    All = WhiteKing | WhiteQueen | BlackKing | BlackQueen
}
public enum Color : byte { None = 0, White = 1, Black = 2 }
public enum PieceType : byte
{
    None = 0,
    Pawn = 1,
    Knight = 2,
    Bishop = 3,
    Rook = 4,
    Queen = 5,
    King = 6
}
public enum Piece : byte
{
    None = 0,

    WhitePawn = 0x11,
    WhiteKnight = 0x12,
    WhiteBishop = 0x13,
    WhiteRook = 0x14,
    WhiteQueen = 0x15,
    WhiteKing = 0x16,

    BlackPawn = 0x21,
    BlackKnight = 0x22,
    BlackBishop = 0x23,
    BlackRook = 0x24,
    BlackQueen = 0x25,
    BlackKing = 0x26,
}


public record Game(Position InitialPosition, Move[] Moves)
{
    public Game() : this(new Position(), []) { }

    public Game(string fen) : this(Position.FromFen(fen), [])
    {
    }

    public int PlyCount => Moves.Length;
    public Color CurrentPlayer => CurrentPosition.CurrentPlayer;
    public Position CurrentPosition => GetPosition(InitialPosition, Moves);

    public Position GetPosition(Position position, Move[] moves)
    {
        foreach (var m in moves) position = position.Move(m);

        return position;
    }

    public bool IsLegalMove(Move move)
    {
        return CurrentPosition
            .GenerateLegalMoves().ToArray()
            .Contains(move);
    }
}

public class Engine
{
    const int Max_Depth = 12;
    private static readonly TranspositionTable tt = new TranspositionTable();
    public static Game NewGame() => new Game();

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
        for (Piece i = Piece.WhitePawn; i < Piece.WhiteKing; i++)
        {
            eval += Heuristics.GetPieceValue(i, position[i]);
        }
        for (Piece i = Piece.BlackPawn; i < Piece.BlackKing; i++)
        {
            eval -= Heuristics.GetPieceValue(i, position[i]);
        }
        return eval;
    }

    public static Move? Reply(Game game)
    {
        var timer = new CancellationTokenSource(1_000);
        var bestMove = default(Move?);
        var depth = 1;

        while (depth <= Max_Depth && !timer.Token.IsCancellationRequested)
        {
            bestMove = BestMove(game, depth, timer.Token);
            Console.WriteLine(depth++);
        }

        return bestMove;
    }

    public static Move? BestMove(Game game, int depth, CancellationToken ct)
    {
        Span<Move> legalMoves = stackalloc Move[218];

        var position = game.CurrentPosition;
        var count = MoveGenerator.Legal(ref position, ref legalMoves);
        legalMoves = legalMoves[..count];

        if (count == 0) return null;

        var bestEval = -999_999;
        var bestMove = legalMoves[0];

        var alpha = -999_999;
        var beta = 999_999;

        for (int i = 0; i < count; i++)
        {
            var move = legalMoves[i];
            var eval = -EvaluateMove(position.Move(move), depth, -beta, -alpha, 1);

            if (eval > bestEval)
            {
                bestEval = eval;
                bestMove = move;
                alpha = Max(alpha, eval);
            }
        }


        return bestMove;
    }

    public static int EvaluateMove(Position position, int depth, int alpha, int beta, int color)
    {
        var eval = -999_999;
        var alphaOrig = alpha;

        if (tt.TryGet(position.Hash, depth, out var ttEntry))
        {
            if (ttEntry.Type == TranspositionTable.Exact)
                return ttEntry.Evaluation;
            else if (ttEntry.Type == TranspositionTable.Beta)
                alpha = Max(alpha, ttEntry.Evaluation);
            else if (ttEntry.Type == TranspositionTable.Alpa)
                beta = Min(beta, ttEntry.Evaluation);

            if (alpha >= beta)
                return ttEntry.Evaluation;
        }

        Span<Move> moves = stackalloc Move[218];
        var count = MoveGenerator.Legal(ref position, ref moves);

        if (count == 0) return eval - depth;
        if (depth == 0) return color * Evaluate(position);

        moves = moves[..count];
        moves.Sort(MoveComparer);

        for (byte i = 0; i < count; i++)
        {
            eval = Max(eval, -EvaluateMove(position.Move(moves[i]), depth - 1, -beta, -alpha, -color));
            alpha = Max(eval, alpha);
            if (alpha >= beta) break;
        }

        byte ttType;
        if (eval <= alphaOrig) ttType = TranspositionTable.Alpa;
        else if (eval >= beta) ttType = TranspositionTable.Beta;
        else ttType = TranspositionTable.Exact;

        tt.Add(position.Hash, depth, eval, ttType, new Core.Move());

        return eval;
    }

    // function negamax(node, depth, α, β, color) is
    // alphaOrig := α

    // (* Transposition Table Lookup; node is the lookup key for ttEntry *)
    // ttEntry := transpositionTableLookup(node)
    // if ttEntry is valid and ttEntry.depth ≥ depth then
    //     if ttEntry.flag = EXACT then
    //         return ttEntry.value
    //     else if ttEntry.flag = LOWERBOUND then
    //         α := max(α, ttEntry.value)
    //     else if ttEntry.flag = UPPERBOUND then
    //         β := min(β, ttEntry.value)

    //     if α ≥ β then
    //         return ttEntry.value

    // if depth = 0 or node is a terminal node then
    //     return color × the heuristic value of node

    // childNodes := generateMoves(node)
    // childNodes := orderMoves(childNodes)
    // value := −∞
    // for each child in childNodes do
    //     value := max(value, −negamax(child, depth − 1, −β, −α, −color))
    //     α := max(α, value)
    //     if α ≥ β then
    //         break

    // (* Transposition Table Store; node is the lookup key for ttEntry *)
    // ttEntry.value := value
    // if value ≤ alphaOrig then
    //     ttEntry.flag := UPPERBOUND
    // else if value ≥ β then
    //     ttEntry.flag := LOWERBOUND
    // else
    //     ttEntry.flag := EXACT
    // ttEntry.depth := depth
    // ttEntry.is_valid := true
    // transpositionTableStore(node, ttEntry)

    // return value

    // public static int EvaluateMove(Position position, int depth, int remainingDepth)
    // {
    //     if (remainingDepth == 0) return Evaluate(position);

    //     var (alpha, beta) = (-999_999, 999_999);

    //     if (position.CurrentPlayer == Color.White)
    //     {
    //         return AlphaBetaMax(position, alpha, beta, depth, remainingDepth);
    //     }
    //     else
    //     {
    //         return AlphaBetaMin(position, alpha, beta, depth, remainingDepth);
    //     }
    // }

    // private static int AlphaBetaMax(Position position, int alpha, int beta, int depth, int remainingDepth)
    // {
    //     if (remainingDepth == 0) return Evaluate(position);

    //     Span<Move> moves = stackalloc Move[218];
    //     var count = MoveGenerator.Legal(ref position, ref moves);
    //     if (count == 0) return -999_999;

    //     moves = moves[..count];
    //     moves.Sort(MoveComparer);
    //     var bestMove = moves[0];

    //     for (byte i = 0; i < count; i++)
    //     {
    //         if (tt.TryGet(position.Hash, depth, ref bestMove, ref alpha, ref beta))
    //         {
    //             Console.WriteLine(Convert.ToHexString(BitConverter.GetBytes(position.Hash)));
    //             Console.WriteLine(tt.Get(position.Hash));
    //             return alpha;
    //         }
    //         var score = AlphaBetaMin(position.Move(moves[i]), alpha, beta, depth, remainingDepth - 1);

    //         if (score >= beta)
    //         {
    //             return beta;   // fail hard beta-cutoff
    //         }
    //         if (score > alpha)
    //         {
    //             alpha = score; // alpha acts like max in MiniMax
    //             bestMove = moves[i];

    //             tt.Add(position.Hash, remainingDepth, alpha, TranspositionTable.Exact, bestMove);
    //         }
    //     }
    //     return alpha;
    // }

    // private static int AlphaBetaMin(Position position, int alpha, int beta, int depth, int remainingDepth)
    // {
    //     if (remainingDepth == 0) return Evaluate(position);

    //     Span<Move> moves = stackalloc Move[218];
    //     var count = MoveGenerator.Legal(ref position, ref moves);
    //     if (count == 0) return 999_999;

    //     moves = moves[..count];
    //     moves.Sort(MoveComparer);
    //     var bestMove = moves[0];

    //     for (byte i = 0; i < count; i++)
    //     {
    //         if (tt.TryGet(position.Hash, depth, ref bestMove, ref alpha, ref beta))
    //         {
    //             return beta;
    //         }
    //         var score = AlphaBetaMax(position.Move(moves[i]), alpha, beta, depth, remainingDepth - 1);
    //         if (score <= alpha)
    //             return alpha; // fail hard alpha-cutoff
    //         if (score < beta)
    //         {
    //             beta = score; // beta acts like min in MiniMax
    //             bestMove = moves[i];
    //             tt.Add(position.Hash, remainingDepth, beta, TranspositionTable.Exact, bestMove);
    //         }
    //     }

    //     return beta;
    // }

    private static int MoveComparer(Move x, Move y)
    {
        int score = 0;
        score += Heuristics.GetPieceValue(x.PromotionPiece);
        score += Heuristics.GetPieceValue(x.CapturePiece);

        score -= Heuristics.GetPieceValue(y.PromotionPiece);
        score -= Heuristics.GetPieceValue(y.CapturePiece);

        return score;
    }
}