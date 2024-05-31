using System.Text;

namespace Chess.Api;

public enum Color : byte { Black = 0, White = 1}

public record Capture(char Name, Square Square)
{
    public static readonly Capture None = new('-', 0);
}

public record Move(Square From, Square To, Capture capture)
{
    public Move(Square From, Square To, Capture capture, Square castleFrom, Square castleTo)
        : this(From, To, capture)
    {
        CastleFrom = castleFrom;
        CastleTo = castleTo;
    }

    public Square CastleFrom { get; }
    public Square CastleTo { get; }

    public static Move Castle(bool isWhitesTurn)
    {
        var rank = isWhitesTurn ? 1 : 8;

        return new Move(
            Utils.SquareFromCoordinates($"E{rank}"),
            Utils.SquareFromCoordinates($"F{rank}"),
            Capture.None,
            Utils.SquareFromCoordinates($"H{rank}"),
            Utils.SquareFromCoordinates($"F{rank}")
        );
    }

    public static Move QueenSideCastle(bool isWhitesTurn)
    {
        var rank = isWhitesTurn ? 1 : 8;

        return new Move(
            Utils.SquareFromCoordinates($"E{rank}"),
            Utils.SquareFromCoordinates($"C{rank}"),
            Capture.None,
            Utils.SquareFromCoordinates($"A{rank}"),
            Utils.SquareFromCoordinates($"D{rank}")
        );
    }
}

public record Position
{
    public ulong WhitePawns { get; init; } = 0x0101010101010101ul << 1;
    public ulong WhiteRooks { get; init; } = Utils.Bitboard("A1", "H1");
    public ulong WhiteBishops { get; init; } = Utils.Bitboard("C1", "F1");
    public ulong WhiteKnights { get; init; } = Utils.Bitboard("B1", "G1");
    public ulong WhiteQueens { get; init; } = Utils.Bitboard("D1");
    public ulong WhiteKing { get; init; } = Utils.Bitboard("E1");

    public ulong BlackPawns { get; init; } = 0x0101010101010101ul << 6;
    public ulong BlackRooks { get; init; } = Utils.Bitboard("A8", "H8");
    public ulong BlackBishops { get; init; } = Utils.Bitboard("C8", "F8");
    public ulong BlackKnights { get; init; } = Utils.Bitboard("B8", "G8");
    public ulong BlackQueens { get; init; } = Utils.Bitboard("D8");
    public ulong BlackKing { get; init; } = Utils.Bitboard("E8");


    public ulong White => Utils.Bitboard(WhitePawns, WhiteRooks, WhiteKnights, WhiteBishops, WhiteQueens, WhiteKing);
    public ulong Black => Utils.Bitboard(BlackPawns, BlackRooks, BlackKnights, BlackBishops, BlackQueens, BlackKing);

    public ulong Occupied => Utils.Bitboard(White, Black);

    public string ToPartialFENString()
    {
        var sb = new StringBuilder(72);
        for (char rank = '8'; rank > '0'; rank--)
        {
            for (char file = 'A'; file <= 'H'; file++)
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

                else
                {
                    if (char.IsDigit(sb[^1])) sb[^1]++;
                    else sb.Append('1');
                }
            }
            sb.Append('/');
        }
        return sb.ToString();
    }

    public override string ToString()
    {
        var sb = new StringBuilder(72);
        for (char rank = '8'; rank > '0'; rank--)
        {
            for (char file = 'A'; file <= 'H'; file++)
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
            WhiteRooks = ApplyMove(WhiteRooks, m),
            WhiteQueens = ApplyMove(WhiteQueens, m),
            WhiteKing = ApplyMove(WhiteKing, m),

            BlackPawns = ApplyMove(BlackPawns, m),
            BlackBishops = ApplyMove(BlackBishops, m),
            BlackKnights = ApplyMove(BlackKnights, m),
            BlackRooks = ApplyMove(BlackRooks, m),
            BlackQueens = ApplyMove(BlackQueens, m),
            BlackKing = ApplyMove(BlackKing, m),
        };
    }

    internal Square[] GetLegalMoves(Square sq)
    {
        return [
            (sq << 7) & Black,
            (sq << 9) & Black
        ];
    }

    private static ulong ApplyMove(ulong bitboard, Move m)
    {
        if ((bitboard & m.capture.Square) != 0)
        {
            bitboard ^= m.capture.Square;
        }

        if ((bitboard & m.From) != 0)
        {
            bitboard = (bitboard ^ m.From) | m.To;
        }

        return bitboard;
    }
}

public record Game(Position InitialPosition, Move[] Moves)
{
    public Game() : this(new Position(), []) { }

    public int PlyCount => Moves.Length;
    public bool IsWhitesTurn => PlyCount % 2 == 0;
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
        var move = new Move(from, to, GetCapture(game, from, to));
        return new Game(game.InitialPosition, [.. game.Moves, move]);
    }

    private static Capture GetCapture(Game game, Square from, Square to)
    {
        if ((game.CurrentPosition.BlackPawns & to) != 0)
            return new Capture('p', to);
        if ((game.CurrentPosition.BlackBishops & to) != 0)
            return new Capture('b', to);
        if ((game.CurrentPosition.BlackKnights & to) != 0)
            return new Capture('n', to);
        if ((game.CurrentPosition.BlackRooks & to) != 0)
            return new Capture('r', to);
        if ((game.CurrentPosition.BlackQueens & to) != 0)
            return new Capture('q', to);

        if ((game.CurrentPosition.WhitePawns & to) != 0)
            return new Capture('P', to);
        if ((game.CurrentPosition.WhiteBishops & to) != 0)
            return new Capture('B', to);
        if ((game.CurrentPosition.WhiteKnights & to) != 0)
            return new Capture('N', to);
        if ((game.CurrentPosition.WhiteRooks & to) != 0)
            return new Capture('R', to);
        if ((game.CurrentPosition.WhiteQueens & to) != 0)
            return new Capture('Q', to);

        return Capture.None;
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