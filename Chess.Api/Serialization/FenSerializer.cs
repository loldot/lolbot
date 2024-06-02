using System.Text;
namespace Chess.Api;

public class FenSerializer
{
    public string ToFenString(Position p)
    {
        var sb = new StringBuilder(72);
        for (char rank = '8'; rank > '0'; rank--)
        {
            for (char file = 'a'; file <= 'h'; file++)
            {
                var sq = Utils.SquareFromCoordinates("" + file + rank);

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
}