using System.Collections.Specialized;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;

namespace Lolbot.Core;

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


[StructLayout(LayoutKind.Sequential)]
public readonly struct Move : IEquatable<Move>
{
    public readonly byte FromIndex;
    public readonly byte ToIndex;
    public readonly byte CaptureIndex = 0;
    public readonly byte CastleIndex = 0;
    public readonly Piece CapturePiece = Piece.None;
    public readonly Piece PromotionPiece = Piece.None;

    public Move(string from, string to) : this(
        Squares.FromCoordinates(from),
        Squares.FromCoordinates(to)
    )
    { }

    public Move(Square from, Square to)
    {
        FromIndex = (byte)BitOperations.Log2(from);
        ToIndex = (byte)BitOperations.Log2(to);
    }

    public Move(byte fromIndex, byte toIndex)
    : this(fromIndex, toIndex, 0, 0, Piece.None, Piece.None) { }

    public Move(byte fromIndex, byte toIndex, byte captureIndex, Piece capturePiece)
    : this(fromIndex, toIndex, captureIndex, 0, capturePiece, Piece.None) { }


    public Move(Square from, Square to, Square captureSquare, Piece capturePiece) : this(from, to)
    {
        CaptureIndex = (byte)BitOperations.Log2(captureSquare);
        CapturePiece = capturePiece;
    }

    public Move(
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

    public override string ToString()
    {
        return $"{Squares.CoordinateFromIndex(FromIndex)}{Squares.CoordinateFromIndex(ToIndex)}";
    }

    public bool Equals(Move other)
    {
        return FromIndex == other.FromIndex
            && ToIndex == other.ToIndex
            && CaptureIndex == other.CaptureIndex
            && CastleIndex == other.CastleIndex
            && CapturePiece == other.CapturePiece
            && PromotionPiece == other.PromotionPiece;
    }

    public override bool Equals(object? obj)
    {
        return obj is Move && Equals((Move)obj);
    }

    public static bool operator ==(Move left, Move right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(Move left, Move right)
    {
        return !(left == right);
    }

    public override int GetHashCode()
    {
        int firstHalf = FromIndex << 26 | ToIndex << 20;
        int secondHalf = CastleIndex + CaptureIndex + (int)CapturePiece + (int)PromotionPiece;
        return firstHalf | (secondHalf & 0xffffff);
    }
}
public record struct Position
{
    public ulong WhitePawns { get; init; } = 0x000000000000ff00;
    public ulong WhiteRooks { get; init; } = Bitboards.Create("A1", "H1");
    public ulong WhiteBishops { get; init; } = Bitboards.Create("C1", "F1");
    public ulong WhiteKnights { get; init; } = Bitboards.Create("B1", "G1");
    public ulong WhiteQueens { get; init; } = Bitboards.Create("D1");
    public ulong WhiteKing { get; init; } = Bitboards.Create("E1");

    public ulong BlackPawns { get; init; } = 0x00ff000000000000;
    public ulong BlackRooks { get; init; } = Bitboards.Create("A8", "H8");
    public ulong BlackBishops { get; init; } = Bitboards.Create("C8", "F8");
    public ulong BlackKnights { get; init; } = Bitboards.Create("B8", "G8");
    public ulong BlackQueens { get; init; } = Bitboards.Create("D8");
    public ulong BlackKing { get; init; } = Bitboards.Create("E8");
    public byte EnPassant { get; init; } = 0;

    public Position()
    {

    }

    public ulong this[Piece piece]
    {
        get => piece switch
        {
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
            _ => Empty,
        };
    }

    public readonly ulong White => Bitboards.Create(WhitePawns, WhiteRooks, WhiteKnights, WhiteBishops, WhiteQueens, WhiteKing);
    public readonly ulong Black => Bitboards.Create(BlackPawns, BlackRooks, BlackKnights, BlackBishops, BlackQueens, BlackKing);

    public readonly ulong Occupied => Bitboards.Create(White, Black);
    public readonly ulong Empty => ~Occupied;

    public override string ToString()
    {
        var sb = new StringBuilder(72);
        for (char rank = '8'; rank > '0'; rank--)
        {
            for (char file = 'a'; file <= 'h'; file++)
            {
                var sq = Squares.FromCoordinates("" + file + rank);
                foreach (var p in Enum.GetValues<Piece>())
                {
                    if ((sq & this[p]) != 0) sb.Append(Utils.PieceName(p));
                }
            }
            sb.AppendLine();
        }
        return sb.ToString();
    }

    public Position Move(Move m)
    {
        return this with
        {
            EnPassant = SetEnPassant(m),
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

    public readonly byte SetEnPassant(Move m)
    {
        var from = 1ul << m.FromIndex;
        var to = 1ul << m.ToIndex;

        // Pawn moves leading to en passant has the en passant square
        // 1 square in front of the start and 1 square behind the target
        var fromWhite = from & WhitePawns;
        var enPassant = (fromWhite << 8) & (to >> 8);

        var fromBlack = from & BlackPawns;
        enPassant ^= (fromBlack >> 8) & (to << 8);

        return Squares.ToIndex(enPassant);
    }

    private static ulong ApplyMove(ulong bitboard, Move m)
    {
        if (m.CapturePiece != Piece.None)
        {
            bitboard &= ~Squares.FromIndex(m.CaptureIndex);
        }

        var fromSq = Squares.FromIndex(m.FromIndex);
        if ((bitboard & fromSq) != 0)
        {
            bitboard ^= fromSq;
            bitboard |= Squares.FromIndex(m.ToIndex);
        }

        return bitboard;
    }

    private static ulong Castle(ulong mask, Move m)
        => mask & Squares.FromIndex(m.CastleIndex);

    public Move[] GenerateLegalMoves(Color color, Piece? pieceType)
    {
        var moves = new Move[218];
        var count = 0;

        if (!pieceType.HasValue || ((int)pieceType.Value & 0xf) == 1)
            count += AddPawnMoves(color, ref moves);
        if (!pieceType.HasValue || ((int)pieceType.Value & 0xf) == 2)
            count += AddKnightMoves(color, ref moves);
        if (!pieceType.HasValue || ((int)pieceType.Value & 0xf) == 3)
            count += AddBishopMoves(color, ref moves);
        if (!pieceType.HasValue || ((int)pieceType.Value & 0xf) == 4)
            count += AddRookMoves(color, ref moves);
        if (!pieceType.HasValue || ((int)pieceType.Value & 0xf) == 6)
            count += AddKingMoves(color, ref moves);


        Array.Resize(ref moves, count);

        return moves;
    }

    private int GetPseudoLegalMove(ref Move[] moves, ulong[] movepattern, ulong pieces, ulong targets)
    {
        var count = 0;
        while (pieces != 0)
        {
            var fromIndex = Bitboards.PopLsb(ref pieces);

            var quiets = MovePatterns.Rooks[fromIndex] & ~Occupied;
            while (quiets != 0)
            {
                var toIndex = Bitboards.PopLsb(ref quiets);
                moves[count++] = new Move(fromIndex, toIndex);
            }

            var attacks = MovePatterns.Rooks[fromIndex] & targets;
            while (attacks != 0)
            {
                var attack = Bitboards.PopLsb(ref attacks);
                moves[count++] = new Move(fromIndex, attack, attack, GetOccupant(attack));
            }
        }
        return count;
    }

    private int AddKingMoves(Color color, ref Move[] moves)
    {
        var count = 0;
        var (rooks, targets) = (color == Color.White)
            ? (WhiteKing, Black)
            : (BlackKing, White);

        while (rooks != 0)
        {
            var fromIndex = Bitboards.PopLsb(ref rooks);

            var quiets = MovePatterns.Kings[fromIndex] & ~Occupied;
            while (quiets != 0)
            {
                var toIndex = Bitboards.PopLsb(ref quiets);
                moves[count++] = new Move(fromIndex, toIndex);
            }

            var attacks = MovePatterns.Kings[fromIndex] & targets;
            while (attacks != 0)
            {
                var attack = Bitboards.PopLsb(ref attacks);
                moves[count++] = new Move(fromIndex, attack, attack, GetOccupant(attack));
            }
        }
        return count;
    }

    private int AddRookMoves(Color color, ref Move[] moves)
    {
        var count = 0;
        var (rooks, targets, friendlies) = (color == Color.White)
            ? (WhiteRooks, Black, White)
            : (BlackRooks, White, Black);

        while (rooks != 0)
        {
            var fromIndex = Bitboards.PopLsb(ref rooks);
            var from = Squares.FromIndex(fromIndex);

            var valid = MovePatterns.RookAttacks(from, Empty) & ~friendlies;
            var quiets = valid & ~targets;
            while (quiets != 0)
            {
                var toIndex = Bitboards.PopLsb(ref quiets);
                moves[count++] = new Move(fromIndex, toIndex);
            }

            var attacks = valid & targets;
            while (attacks != 0)
            {
                var attack = Bitboards.PopLsb(ref attacks);
                moves[count++] = new Move(fromIndex, attack, attack, GetOccupant(attack));
            }
        }
        return count;
    }

    private int AddBishopMoves(Color color, ref Move[] moves)
    {
        var count = 0;
        var (bishops, targets) = (color == Color.White)
            ? (WhiteBishops, Black)
            : (BlackBishops, White);

        while (bishops != 0)
        {
            var fromIndex = Bitboards.PopLsb(ref bishops);

            var quiets = MovePatterns.Bishops[fromIndex] & ~Occupied;
            while (quiets != 0)
            {
                var toIndex = Bitboards.PopLsb(ref quiets);
                moves[count++] = new Move(fromIndex, toIndex);
            }

            var attacks = MovePatterns.Bishops[fromIndex] & targets;
            while (attacks != 0)
            {
                var attack = Bitboards.PopLsb(ref attacks);
                moves[count++] = new Move(fromIndex, attack, attack, GetOccupant(attack));
            }
        }
        return count;
    }

    private int AddKnightMoves(Color color, ref Move[] moves)
    {
        var count = 0;
        var (knights, targets) = (color == Color.White)
            ? (WhiteKnights, Black)
            : (BlackKnights, White);

        while (knights != 0)
        {
            var fromIndex = Bitboards.PopLsb(ref knights);

            var quiets = MovePatterns.Knights[fromIndex] & ~Occupied;
            while (quiets != 0)
            {
                var toIndex = Bitboards.PopLsb(ref quiets);
                moves[count++] = new Move(fromIndex, toIndex);
            }

            var attacks = MovePatterns.Knights[fromIndex] & targets;
            while (attacks != 0)
            {
                var attack = Bitboards.PopLsb(ref attacks);
                moves[count++] = new Move(fromIndex, attack, attack, GetOccupant(attack));
            }
        }
        return count;
    }

    private int AddPawnMoves(Color color, ref Move[] moves)
    {
        int count = 0;

        var (pawns, blockers, targets, pushPattern, attackPattern) = (color == Color.White)
            ? (WhitePawns, White, Black, MovePatterns.WhitePawnPushes, MovePatterns.WhitePawnAttacks)
            : (BlackPawns, Black, White, MovePatterns.BlackPawnPushes, MovePatterns.BlackPawnAttacks);

        while (pawns != 0)
        {
            var sq = Bitboards.PopLsb(ref pawns);

            var pushes = pushPattern[sq] & ~blockers;
            while (pushes != 0)
            {
                var push = Bitboards.PopLsb(ref pushes);
                moves[count++] = new Move(sq, push);
            }

            var attacks = attackPattern[sq] & (targets | (1ul << EnPassant));
            while (attacks != 0)
            {
                var attack = Bitboards.PopLsb(ref attacks);
                moves[count++] = new Move(sq, attack, attack, GetOccupant(attack));
            }
        }
        return count;
    }

    public Piece GetOccupant(ulong attack)
    {
        foreach (var type in Enum.GetValues<Piece>())
        {
            if ((this[type] & attack) != 0) return type;
        }
        return Piece.None;
    }
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
            Squares.FromCoordinates(from),
            Squares.FromCoordinates(to)
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
        return game.CurrentPosition.GetOccupant(to);
    }

    public static int Evaluate(Position position)
    {
        return Bitboards.CountOccupied(position.WhitePawns) * 100
            + Bitboards.CountOccupied(position.WhiteKnights) * 300
            + Bitboards.CountOccupied(position.WhiteBishops) * 325
            + Bitboards.CountOccupied(position.WhiteRooks) * 500
            + Bitboards.CountOccupied(position.BlackQueens) * 900

            + Bitboards.CountOccupied(position.BlackPawns) * -100
            + Bitboards.CountOccupied(position.BlackKnights) * -300
            + Bitboards.CountOccupied(position.BlackBishops) * -325
            + Bitboards.CountOccupied(position.BlackRooks) * -500
            + Bitboards.CountOccupied(position.BlackQueens) * -900;
    }
}