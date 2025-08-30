using Lolbot.Core;

namespace Lolbot.Api;

public static class ApiMove
{
    public static string?[] Create(Move move)
    {
        string?[] basic = [
                Squares.ToCoordinate(move.FromSquare),
                Squares.ToCoordinate(move.ToSquare),
        ];

        if (move.CastleFlag != CastlingRights.None)
        {
            return [
                ..basic,
                Squares.ToCoordinate(move.CaptureSquare),
                Utils.PieceName(move.CapturePiece).ToString(),
                Squares.ToCoordinate(move.CastleSquare),
            ];
        }
        
        if (move.CaptureIndex != 0)
        {
            return [
                ..basic,
                Squares.ToCoordinate(move.CaptureSquare),
                Utils.PieceName(move.CapturePiece).ToString()
            ];
        }


        return basic;
    }
}

public class ApiGame
{
    public ApiGame(Game game)
    : this(new MutablePosition(), game.Moves) { }

    public ApiGame(MutablePosition position, Move[] moves)
    {
        InitialPosition = new ApiPosition(position);
        Moves = moves.Select(ApiMove.Create)
            .ToArray();
    }

    public ApiPosition InitialPosition { get; }
    public string?[][] Moves { get; } = [];
}