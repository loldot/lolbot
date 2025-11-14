using System.Text;
namespace Lolbot.Core;

public static class FenSerializer
{
    public static string ToFenString(MutablePosition p)
    {
        var sb = new StringBuilder(72);
        for (char rank = '8'; rank > '0'; rank--)
        {
            for (char file = 'a'; file <= 'h'; file++)
            {
                var sq = Squares.IndexFromCoordinate("" + file + rank);

                Piece piece = p.GetOccupant(ref sq);

                if (piece != Piece.None)
                {
                    sb.Append(Utils.PieceName(piece));
                    continue;
                }

                if (sb.Length > 0 && char.IsDigit(sb[^1])) sb[^1]++;
                else sb.Append('1');
            }
            sb.Append('/');
        }
        sb.Length--; // remove last /
        sb.Append(' ');

        sb.Append(p.CurrentPlayer == Colors.White ? 'w' : 'b');
        sb.Append(' ');

        sb.Append(p.CastlingRights == CastlingRights.None ? "-" : Utils.CastlingRightsToString(p.CastlingRights));
        sb.Append(' ');

        sb.Append(p.EnPassant == 0 ? "-" : Squares.CoordinateFromIndex(p.EnPassant));
        sb.Append(' ');

        sb.Append(p.plyfromRoot / 2);
        sb.Append(' ');

        sb.Append(p.plyfromRoot);

        return sb.ToString();
    }

    public static MutablePosition Parse(string fenString)
    {
        var position = MutablePosition.EmptyBoard;

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
                position[piece] = position[piece] | Squares.FromCoordinates($"{file}{rank}");
                file++;
            }

            token = fenString[++i];
        } while (token != ' ');

        var metaTokens = fenString[(i + 1)..].Split(' ');
        var currentPlayer = metaTokens[0] == "w" ? Colors.White : Colors.Black;

        position.EnPassant = ParseEnPassantSquare(metaTokens[2]);
        position.CastlingRights = ParseCastlingRights(metaTokens[1]);
        position.CurrentPlayer = currentPlayer;

        position.Reevaluate();

        position.Hash = Hashes.New(position);

        return position;
    }

    private static byte ParseEnPassantSquare(string epSquare)
    {
        return epSquare == "-"
            ? (byte)0
            : Squares.IndexFromCoordinate(epSquare);
    }

    private static CastlingRights ParseCastlingRights(string fenCastlingRights)
    {
        var castlingRights = CastlingRights.None;
        foreach (var c in fenCastlingRights)
        {
            castlingRights |= c switch
            {
                'K' => CastlingRights.WhiteKing,
                'Q' => CastlingRights.WhiteQueen,
                'k' => CastlingRights.BlackKing,
                'q' => CastlingRights.BlackQueen,
                _ => CastlingRights.None
            };
        }
        return castlingRights;
    }
}