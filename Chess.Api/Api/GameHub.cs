using Lolbot.Core;
using Microsoft.AspNetCore.SignalR;

namespace Lolbot.Api;

public class MoveMessage
{
    public required int GameId { get; set; }
    public required int PlyCount { get; set; }
    public required string[] Move { get; set; }
}

public class CheckMovesMessage
{
    public required int GameId { get; set; }
    public required string Square { get; set; }
}

public class GameHub : Hub
{
    // public async Task Move(MoveMessage message)
    // {
        // var game = GameDatabase.Instance.Get(message.GameId);
        // if (game is null) return;

        // Engine.Move(game, message.Move[0], message.Move[1]);
        // GameDatabase.Instance.Update(message.GameId, updated);
        // await Clients.AllExcept([Context.ConnectionId]).SendAsync("movePlayed", message);

        // var nextMove = Engine.BestMove(updated);
        // if (nextMove is null)
        // {
        //     await Clients.All.SendAsync("finished", new { Winner = "w" });
        //     return;
        // }

        // updated = Engine.Move(updated, nextMove.Value);
        // GameDatabase.Instance.Update(message.GameId, updated);

        // await Clients.All.SendAsync("movePlayed", new MoveMessage
        // {
        //     GameId = message.GameId,
        //     PlyCount = updated.PlyCount,
        //     Move = ApiMove.Create(nextMove.Value)!
        // });
    // }

    public async Task CheckMove(CheckMovesMessage message)
    {
        var game = GameDatabase.Instance.Get(message.GameId);
        if (game is null) return;

        var fromIndex = Squares.IndexFromCoordinate(message.Square);
        var legalMoves = game.CurrentPosition.GenerateLegalMoves().ToArray();
        var moves = new List<string>();
        for (int i = 0; i < legalMoves.Length; i++)
        {
            var x = legalMoves[i];

            if (fromIndex != x.FromIndex) continue;

            moves.Add(Squares.ToCoordinate(x.ToSquare)!);
        }

        await Clients.Caller.SendAsync("legalMovesReceived", moves);
    }
}