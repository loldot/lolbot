namespace Lolbot.Core;

public class ApiPosition : Dictionary<string, char>
{
    public ApiPosition(MutablePosition position)
    {
        for (char file = 'a'; file <= 'h'; file++)
        {
            for (char rank = '8'; rank > '0'; rank--)
            {
                var coordinate = "" + file + rank;
                var sq = Squares.FromCoordinates(coordinate);

                if ((sq & position.BlackPawns) != 0) Add(coordinate, 'p');
                else if ((sq & position.BlackRooks) != 0) Add(coordinate, 'r');
                else if ((sq & position.BlackBishops) != 0) Add(coordinate, 'b');
                else if ((sq & position.BlackKnights) != 0) Add(coordinate, 'n');
                else if ((sq & position.BlackQueens) != 0) Add(coordinate, 'q');
                else if ((sq & position.BlackKing) != 0) Add(coordinate, 'k');

                else if ((sq & position.WhitePawns) != 0) Add(coordinate, 'P');
                else if ((sq & position.WhiteRooks) != 0) Add(coordinate, 'R');
                else if ((sq & position.WhiteBishops) != 0) Add(coordinate, 'B');
                else if ((sq & position.WhiteKnights) != 0) Add(coordinate, 'N');
                else if ((sq & position.WhiteQueens) != 0) Add(coordinate, 'Q');
                else if ((sq & position.WhiteKing) != 0) Add(coordinate, 'K');
            }
        }
    }
}