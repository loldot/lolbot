using System.Collections;
using System.Text;

namespace Chess.Api;

public record Capture(char Name, Square Square)
{
    public static readonly Capture None = new('-', 0);
}

public record Move(Square From, Square To, Capture capture);
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

    public Position CurrentPosition => GetPosition(InitialPosition, Moves);

    public Position GetPosition(Position position, Move[] moves)
    {
        foreach (var m in moves)
        {
            position = position.Move(m);
        }
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

    public static Game Parse(string pgn)
    {
        var game = new Game();
        return new Game();
    }
}

public static class Utils
{
    public static char GetFile(Square square)
    {
        char[] files = ['A', 'B', 'C', 'D', 'E', 'F', 'G', 'H'];
        ulong mask = 0xFFul;
        for (int i = 0; i < files.Length; i++)
        {
            if ((mask & square) != 0) return files[i];
            mask <<= 8;
        }
        return '-';
    }

    public static byte GetRank(Square square)
    {
        ulong mask = 0x0101010101010101ul;
        for (byte i = 1; i <= 8; i++)
        {
            if ((mask & square) != 0) return i;
            mask <<= 1;
        }
        return 0;
    }


    // Little-Endian File-Rank Mapping
    public static Square SquareFromCoordinates(string coords)
    {
        byte file = (byte)(coords[0] - 'A');
        byte rank = (byte)(coords[1] - '1');

        return 1ul << (file * 8 + rank);
    }

    public static ulong Bitboard(params string[] squares)
    {
        ulong board = 0;
        for (int i = 0; i < squares.Length; i++)
        {
            board |= SquareFromCoordinates(squares[i]);
        }
        return board;
    }

    public static ulong Bitboard(params ulong[] squares)
    {
        ulong board = 0;
        for (int i = 0; i < squares.Length; i++)
        {
            board |= squares[i];
        }
        return board;
    }

    public static int CountBits(ulong v)
    {
        int c; // c accumulates the total bits set in v
        for (c = 0; v != 0; c++)
        {
            v &= v - 1; // clear the least significant bit set
        }
        return c;
    }
}