using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;

namespace Chess.Api;

public enum Color : byte { None = 0, White = 1, Black = 2 }
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


[StructLayout(LayoutKind.Auto)]
public readonly struct Move
{
    public readonly byte FromIndex;
    public readonly byte ToIndex;
    public readonly byte CaptureIndex = 0;
    public readonly byte CastleIndex = 0;
    public readonly Piece CapturePiece = Piece.None;
    public readonly Piece PromotionPiece = Piece.None;

    public Move(Square from, Square to)
    {
        FromIndex = (byte)BitOperations.Log2(from);
        ToIndex = (byte)BitOperations.Log2(to);
    }


    public Move(Square from, Square to, Square captureSquare, Piece capturePiece) : this(from, to)
    {
        CaptureIndex = (byte)BitOperations.Log2(captureSquare);
        CapturePiece = capturePiece;
    }

    private Move(
        byte fromIndex,
        byte toIndex,
        byte captureIndex,
        byte castleIndex,
        Piece capturePiece,
        Piece promotionPiece)
    {
        FromIndex = fromIndex;
        ToIndex = toIndex;
        CaptureIndex = captureIndex;
        CastleIndex = castleIndex;
        CapturePiece = capturePiece;
        PromotionPiece = promotionPiece;
    }

    private static readonly Move WhiteCastle = new(4, 6, 7, 5, Piece.WhiteRook, Piece.None);
    private static readonly Move BlackCastle = new(60, 62, 63, 61, Piece.BlackRook, Piece.None);

    private static readonly Move WhiteQueenCastle = new(4, 2, 0, 3, Piece.WhiteRook, Piece.None);
    private static readonly Move BlackQueenCastle = new(60, 62, 63, 61, Piece.BlackRook, Piece.None);


    // TODO: Fisher castling rules :cry:
    public static Move Castle(Color color)
        => color == Color.White ? WhiteCastle : BlackCastle;
    public static Move QueenSideCastle(Color color)
        => color == Color.White ? WhiteQueenCastle : BlackQueenCastle;

}
public record Position
{
    public ulong WhitePawns { get; init; } = 0x000000000000ff00;
    public ulong WhiteRooks { get; init; } = Utils.Bitboard("A1", "H1");
    public ulong WhiteBishops { get; init; } = Utils.Bitboard("C1", "F1");
    public ulong WhiteKnights { get; init; } = Utils.Bitboard("B1", "G1");
    public ulong WhiteQueens { get; init; } = Utils.Bitboard("D1");
    public ulong WhiteKing { get; init; } = Utils.Bitboard("E1");

    public ulong BlackPawns { get; init; } = 0x00ff000000000000;
    public ulong BlackRooks { get; init; } = Utils.Bitboard("A8", "H8");
    public ulong BlackBishops { get; init; } = Utils.Bitboard("C8", "F8");
    public ulong BlackKnights { get; init; } = Utils.Bitboard("B8", "G8");
    public ulong BlackQueens { get; init; } = Utils.Bitboard("D8");
    public ulong BlackKing { get; init; } = Utils.Bitboard("E8");
    public ulong EnPassant { get; init; } = 0;

    public ulong GetBitboard(Piece piece) => piece switch
    {
        Piece.None => Empty,
        Piece.WhitePawn => WhitePawns,
        Piece.WhiteKnight => WhiteKnights,
        Piece.WhiteBishop => WhiteBishops,
        Piece.WhiteRook => WhiteRooks,
        Piece.WhiteQueen => WhiteQueens,
        Piece.WhiteKing => WhiteKing,
        Piece.BlackPawn => BlackPawns,
        Piece.BlackKnight => BlackKnights,
        Piece.BlackBishop => BlackBishops,
        Piece.BlackRook => BlackRooks,
        Piece.BlackQueen => BlackQueens,
        Piece.BlackKing => BlackKing,
        _ => throw new NotImplementedException(),
    };


    public ulong White => Utils.Bitboard(WhitePawns, WhiteRooks, WhiteKnights, WhiteBishops, WhiteQueens, WhiteKing);
    public ulong Black => Utils.Bitboard(BlackPawns, BlackRooks, BlackKnights, BlackBishops, BlackQueens, BlackKing);

    public ulong Occupied => Utils.Bitboard(White, Black);
    public ulong Empty => ~Occupied;

    public override string ToString()
    {
        var sb = new StringBuilder(72);
        for (char rank = '8'; rank > '0'; rank--)
        {
            for (char file = 'a'; file <= 'h'; file++)
            {
                var sq = Utils.SquareFromCoordinates("" + file + rank);

                if ((sq & BlackPawns) != 0) sb.Append('p');
                else if ((sq & BlackRooks) != 0) sb.Append('r');
                else if ((sq & BlackBishops) != 0) sb.Append('b');
                else if ((sq & BlackKnights) != 0) sb.Append('n');
                else if ((sq & BlackQueens) != 0) sb.Append('q');
                else if ((sq & BlackKing) != 0) sb.Append('k');

                else if ((sq & WhitePawns) != 0) sb.Append('P');
                else if ((sq & WhiteRooks) != 0) sb.Append('R');
                else if ((sq & WhiteBishops) != 0) sb.Append('B');
                else if ((sq & WhiteKnights) != 0) sb.Append('N');
                else if ((sq & WhiteQueens) != 0) sb.Append('Q');
                else if ((sq & WhiteKing) != 0) sb.Append('K');

                else sb.Append(' ');
            }
            sb.AppendLine();
        }
        return sb.ToString();
    }

    public Position Move(Move m)
    {
        return this with
        {
            WhitePawns = ApplyMove(WhitePawns, m),
            WhiteBishops = ApplyMove(WhiteBishops, m),
            WhiteKnights = ApplyMove(WhiteKnights, m),
            WhiteRooks = ApplyMove(WhiteRooks, m) | Castle(0x7e, m),
            WhiteQueens = ApplyMove(WhiteQueens, m),
            WhiteKing = ApplyMove(WhiteKing, m),

            BlackPawns = ApplyMove(BlackPawns, m),
            BlackBishops = ApplyMove(BlackBishops, m),
            BlackKnights = ApplyMove(BlackKnights, m),
            BlackRooks = ApplyMove(BlackRooks, m) | Castle(BlackRooks & 0x7e << 55, m),
            BlackQueens = ApplyMove(BlackQueens, m),
            BlackKing = ApplyMove(BlackKing, m),
        };
    }

    private static ulong ApplyMove(ulong bitboard, Move m)
    {
        if (m.CapturePiece != Piece.None)
        {
            bitboard &= ~Utils.SquareFromIndex(m.CaptureIndex);
        }

        var fromSq = Utils.SquareFromIndex(m.FromIndex);
        if ((bitboard & fromSq) != 0)
        {
            bitboard ^= fromSq;
            bitboard |= Utils.SquareFromIndex(m.ToIndex);
        }

        return bitboard;
    }

    private ulong Castle(ulong mask, Move m) 
        => mask & Utils.SquareFromIndex(m.CastleIndex);

    public ulong WhitePawnAttacks()
    {
        const ulong notAFileMask = 0xfefefefefefefefe;
        const ulong notHFileMask = 0x7f7f7f7f7f7f7f7f;
        return ((WhitePawns << 7) & notHFileMask)
            | ((WhitePawns << 9) & notAFileMask);
    }
    public ulong WhitePawnForward()
    {
        return WhitePawns << 8 | ((WhitePawns & 0xff00) << 16);
    }

    public ulong LegalMoves(Piece piece)
    {
        if (piece == Piece.WhitePawn)
            return (WhitePawnAttacks() & (Black | EnPassant)) | (WhitePawnForward() & ~Black);

        return 0;
    }

    // public ulong LegalMoves(Piece piece, Square target)
    // {

    // }
}

public record Game(Position InitialPosition, Move[] Moves)
{
    public Game() : this(new Position(), []) { }

    public int PlyCount => Moves.Length;
    public Color CurrentPlayer => (Color)(PlyCount % 2 + 1);
    public Position CurrentPosition => GetPosition(InitialPosition, Moves);

    public Position GetPosition(Position position, Move[] moves)
    {
        foreach (var m in moves) position = position.Move(m);

        return position;
    }

}


public class Engine
{
    public static Game NewGame() => new Game();

    public static Game Move(Game game, string from, string to)
    {
        return Move(
            game,
            Utils.SquareFromCoordinates(from),
            Utils.SquareFromCoordinates(to)
        );
    }

    public static Game Move(Game game, Square from, Square to)
    {
        var capturePiece = GetCapture(game, to);
        var captureSquare = capturePiece != Piece.None ? to : 0;
        var move = new Move(from, to, captureSquare, capturePiece);
        return Move(game, move);
    }

    public static Game Move(Game game, Move move)
    {
        return new Game(game.InitialPosition, [.. game.Moves, move]);
    }

    private static Piece GetCapture(Game game, Square to)
    {
        if ((game.CurrentPosition.BlackPawns & to) != 0)
            return Piece.BlackPawn;
        if ((game.CurrentPosition.BlackBishops & to) != 0)
            return Piece.BlackBishop;
        if ((game.CurrentPosition.BlackKnights & to) != 0)
            return Piece.BlackKnight;
        if ((game.CurrentPosition.BlackRooks & to) != 0)
            return Piece.BlackRook;
        if ((game.CurrentPosition.BlackQueens & to) != 0)
            return Piece.BlackQueen;

        if ((game.CurrentPosition.WhitePawns & to) != 0)
            return Piece.WhitePawn;
        if ((game.CurrentPosition.WhiteBishops & to) != 0)
            return Piece.WhiteBishop;
        if ((game.CurrentPosition.WhiteKnights & to) != 0)
            return Piece.WhiteKnight;
        if ((game.CurrentPosition.WhiteRooks & to) != 0)
            return Piece.WhiteRook;
        if ((game.CurrentPosition.WhiteQueens & to) != 0)
            return Piece.WhiteQueen;

        return Piece.None;
    }

    public static int Evaluate(Position position)
    {
        return Utils.CountBits(position.WhitePawns) * 100
            + Utils.CountBits(position.WhiteKnights) * 300
            + Utils.CountBits(position.WhiteBishops) * 325
            + Utils.CountBits(position.WhiteRooks) * 500
            + Utils.CountBits(position.BlackQueens) * 900

            + Utils.CountBits(position.BlackPawns) * -100
            + Utils.CountBits(position.BlackKnights) * -300
            + Utils.CountBits(position.BlackBishops) * -325
            + Utils.CountBits(position.BlackRooks) * -500
            + Utils.CountBits(position.BlackQueens) * -900;
    }
}