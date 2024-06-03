using Lolbot.Core;

namespace Lolbot.Api;

public static class ApiMove
{
    public static string?[] Create(Move move)
    {
        string?[] basic = [
                Squares.CoordinateFromIndex(move.FromIndex),
                Squares.CoordinateFromIndex(move.ToIndex),
        ];

        if (move.CaptureIndex != 0)
        {
            return [
                ..basic,
                Squares.CoordinateFromIndex(move.CaptureIndex),
                Utils.PieceName(move.CapturePiece).ToString()
            ];
        }
        if (move.CastleIndex != 0)
        {
            return [
                ..basic,
                Squares.CoordinateFromIndex(move.CaptureIndex),
                Utils.PieceName(move.CapturePiece).ToString(),
                Squares.CoordinateFromIndex(move.CastleIndex),
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