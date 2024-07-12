using System.Text;
using Microsoft.AspNetCore.Components.RenderTree;
namespace Lolbot.Core;

public class FenSerializer
{
    public string ToFenString(Position p)
    {
        var sb = new StringBuilder(72);
        for (char rank = '8'; rank > '0'; rank--)
        {
            for (char file = 'a'; file <= 'h'; file++)
            {
                var sq = Squares.FromCoordinates("" + file + rank);

                foreach (var piece in Enum.GetValues<Piece>())
                {
                    if ((sq & p[piece]) != 0)
                    {
                        if (piece != Piece.None)
                        {
                            sb.Append(Utils.PieceName(piece));
                            continue;
                        }

                        if (sb.Length > 0 && char.IsDigit(sb[^1])) sb[^1]++;
                        else sb.Append('1');
                    }
                }
            }
            sb.Append('/');
        }
        return sb.ToString();
    }

    public Position Parse(string fenString)
    {
        var position = Position.EmptyBoard;

        if (string.IsNullOrEmpty(fenString)) return position;

        int i = 0, rank = 8;
        char file = 'a';
        var token = fenString[i];

        do
        {
            if (char.IsDigit(token)) file = (char)(file + (token - '0'));
            if (token == '/') { rank--; file = 'a'; }

            if ("pnbrqk".Contains(token, StringComparison.OrdinalIgnoreCase))
            {
                var piece = Utils.FromName(token);
                var bitboard = position[piece] | Squares.FromCoordinates($"{file}{rank}");

                position = position.Update(piece, bitboard);
                file++;
            }

            token = fenString[++i];
        } while (token != ' ');

        var metaTokens = fenString[(i + 1)..].Split(' ');
        var currentPlayer = metaTokens[0] == "w" ? Color.White : Color.Black;

        var white = Bitboards.Create(position.WhitePawns, position.WhiteRooks, position.WhiteKnights, position.WhiteBishops, position.WhiteQueens, position.WhiteKing);
        var black = Bitboards.Create(position.BlackPawns, position.BlackRooks, position.BlackKnights, position.BlackBishops, position.BlackQueens, position.BlackKing);
        var occupied = Bitboards.Create(white, black);



        position = position with
        {
            CurrentPlayer = currentPlayer,

            White = white,
            Black = black,
            Occupied = occupied,
            Empty = ~occupied,
            CastlingRights = ParseCastlingRights(metaTokens[1]),
            EnPassant = ParseEnPassantSquare(metaTokens[2]),
        };
        var (checkmask, checkers) = position.CreateCheckMask(currentPlayer);

        var (isPinned, pinmasks) = position.CreatePinmasks(currentPlayer);
        return position with
        {
            CheckerCount = checkers,
            Checkmask = checkmask,
            Pinmasks = isPinned ? pinmasks : [0, 0, 0, 0],
        };
    }

    private static byte ParseEnPassantSquare(string epSquare)
    {
        return epSquare == "-"
            ? (byte)0
            : Squares.IndexFromCoordinate(epSquare);
    }

    private static CastlingRights ParseCastlingRights(string fenCastlingRights)
    {
        Dictionary<char, CastlingRights> map = new()
        {
            ['-'] = CastlingRights.None,
            ['K'] = CastlingRights.WhiteKing,
            ['Q'] = CastlingRights.WhiteQueen,
            ['k'] = CastlingRights.BlackKing,
            ['q'] = CastlingRights.BlackQueen,
        };

        var castlingRights = CastlingRights.None;
        foreach (var c in fenCastlingRights)
        {
            castlingRights |= map[c];
        }
        return castlingRights;
    }
}