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
public enum Colors : byte { None = 0, White = 1, Black = 2 }
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

