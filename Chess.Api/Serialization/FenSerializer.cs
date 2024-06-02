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

                if ((sq & p.BlackPawns) != 0) sb.Append('p');
                else if ((sq & p.BlackRooks) != 0) sb.Append('r');
                else if ((sq & p.BlackBishops) != 0) sb.Append('b');
                else if ((sq & p.BlackKnights) != 0) sb.Append('n');
                else if ((sq & p.BlackQueens) != 0) sb.Append('q');
                else if ((sq & p.BlackKing) != 0) sb.Append('k');

                else if ((sq & p.WhitePawns) != 0) sb.Append('P');
                else if ((sq & p.WhiteRooks) != 0) sb.Append('R');
                else if ((sq & p.WhiteBishops) != 0) sb.Append('B');
                else if ((sq & p.WhiteKnights) != 0) sb.Append('N');
                else if ((sq & p.WhiteQueens) != 0) sb.Append('Q');
                else if ((sq & p.WhiteKing) != 0) sb.Append('K');

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
}