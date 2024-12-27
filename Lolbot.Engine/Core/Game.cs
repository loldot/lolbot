using System.Collections.Immutable;

namespace Lolbot.Core;

public sealed class Game
{
    private readonly RepetitionTable repetitions = new();
    private readonly MutablePosition position;
    private readonly Stack<Move> moves = [];
    public Move[] Moves => moves.Reverse().ToArray();

    public Colors CurrentPlayer => position.CurrentPlayer;
    public int PlyCount => moves.Count;
    public MutablePosition CurrentPosition => position;
    public RepetitionTable RepetitionTable => repetitions;

    public Game(Position initialPosition)
    {
        position = MutablePosition.FromReadOnly(initialPosition);
    }

    public Game(MutablePosition initialPosition)
    {
        position = initialPosition;
    }

    public Game(MutablePosition initialPosition, Move[] moves)
    {
        position = initialPosition;
        foreach (var move in moves)
        {
            Move(move);
        }
    }


    public Game(Position initialPosition, Move[] moves)
    {
        position = MutablePosition.FromReadOnly(initialPosition);
        foreach (var move in moves)
        {
            Move(move);
        }
    }

    public Game() : this(new Position())
    {
    }
    public Game(string fen)
    {
        position = MutablePosition.FromFen(fen);
    }

    public void Move(in Move m)
    {
        position.Move(in m);
        repetitions.Update(m, position.Hash);
        moves.Push(m);
    }
    public void UndoLastMove()
    {
        var m = moves.Pop();
        repetitions.Unwind();
        position.Undo(in m);
    }


    public bool IsLegalMove(Move move)
    {
        return position
            .GenerateLegalMoves().ToArray()
            .Contains(move);
    }

    public Move[] GenerateLegalMoves()
    {
        return position.GenerateLegalMoves().ToArray();
    }

    public Move[] GenerateLegalMoves(Piece piece)
    {
        return position.GenerateLegalMoves(piece).ToArray();
    }
}

[Flags]
public enum CastlingRights : byte
{
    None = 0,
    WhiteQueen = 1,
    WhiteKing = 2,
    BlackQueen = 4,
    BlackKing = 8,
    All = WhiteKing | WhiteQueen | BlackKing | BlackQueen
}
public enum Colors : byte { White = 7, Black = 0 }
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

    WhitePawn = (Colors.White << 4) | PieceType.Pawn,
    WhiteKnight = (Colors.White << 4) | PieceType.Knight,
    WhiteBishop = (Colors.White << 4) | PieceType.Bishop,
    WhiteRook = (Colors.White << 4) | PieceType.Rook,
    WhiteQueen = (Colors.White << 4) | PieceType.Queen,
    WhiteKing = (Colors.White << 4) | PieceType.King,

    BlackPawn = (Colors.Black << 4) | PieceType.Pawn,
    BlackKnight = (Colors.Black << 4) | PieceType.Knight,
    BlackBishop = (Colors.Black << 4) | PieceType.Bishop,
    BlackRook = (Colors.Black << 4) | PieceType.Rook,
    BlackQueen = (Colors.Black << 4) | PieceType.Queen,
    BlackKing = (Colors.Black << 4) | PieceType.King,
}

