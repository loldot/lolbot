namespace Chess.Api;

public static class ApiMove
{
    public static string?[] Create(Move move)
    {
        string?[] basic = [
                Utils.CoordinateFromIndex(move.FromIndex),
                Utils.CoordinateFromIndex(move.ToIndex),
        ];

        if (move.CaptureIndex != 0)
        {
            return [
                ..basic,
                Utils.CoordinateFromIndex(move.CaptureIndex),
                Utils.PieceName(move.CapturePiece).ToString()
            ];
        }
        if (move.CastleIndex != 0)
        {
            return [
                ..basic,
                Utils.CoordinateFromIndex(move.CaptureIndex),
                Utils.PieceName(move.CapturePiece).ToString(),
                Utils.CoordinateFromIndex(move.CastleIndex),
            ];
        }

        return basic;
    }
}

public class ApiGame
{
    public ApiGame(Game game)
    : this(game.InitialPosition, game.Moves) { }

    public ApiGame(Position position, Move[] moves)
    {
        InitialPosition = new ApiPosition(position);
        Moves = moves.Select(ApiMove.Create)
            .ToArray();
    }

    public ApiPosition InitialPosition { get; }
    public string?[][] Moves { get; } = [];
}