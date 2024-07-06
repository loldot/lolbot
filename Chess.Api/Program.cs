using Lolbot.Api;
using Lolbot.Core;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSignalR();
builder.Services.AddCors();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseCors(opts =>
    {
        opts.WithOrigins("http://localhost:5173")
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials();
    });
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

await GameDatabase.Instance.Seed();

app.MapHub<GameHub>("/game/realtime");

app.MapPost("/game/new", () =>
{
    var (seq, game) = GameDatabase.Instance.Create();
    return Results.Ok(new { seq, game = new ApiGame(game) });
});

app.MapGet("/game/{seq}/legal-moves/", (int seq) =>
{
    var game = GameDatabase.Instance.Get(seq);
    if (game is null) return Results.NotFound();

    var legalMoves = game.CurrentPosition.GenerateLegalMoves(game.CurrentPlayer);
    var moves = new string[legalMoves.Length][];
    for (int i = 0; i < legalMoves.Length; i++)
    {
        var x = legalMoves[i];
        moves[i] = [Squares.CoordinateFromIndex(x.FromIndex)!, Squares.CoordinateFromIndex(x.ToIndex)!];
    }

    return Results.Ok(moves);
});

app.MapGet("/game/{seq}/legal-moves/{square}/{piece}", (int seq, string square, string piece) =>
{
    var game = GameDatabase.Instance.Get(seq);
    if (game is null) return Results.NotFound();

    var fromIndex = Squares.IndexFromCoordinate(square);
    var legalMoves = game.CurrentPosition.GenerateLegalMoves(game.CurrentPlayer);
    var moves = new List<string>();
    for (int i = 0; i < legalMoves.Length; i++)
    {
        var x = legalMoves[i];

        if (fromIndex != x.FromIndex) continue;

        moves.Add(Squares.CoordinateFromIndex(x.ToIndex)!);
    }

    return Results.Ok(moves);
});
app.MapGet("/game/{seq}", (int seq) =>
{
    var game = GameDatabase.Instance.Get(seq);
    if (game is null) return Results.NotFound();
    return Results.Ok(new ApiGame(game));

})
.WithOpenApi();

app.MapPost("/game/{seq}", (int seq, string[] movedata) =>
{
    var game = GameDatabase.Instance.Get(seq);
    if (game is null) return Results.NotFound();

    var updated = Engine.Move(game, movedata[0], movedata[1]);

    GameDatabase.Instance.Update(seq, updated);
    return Results.Ok(new ApiGame(updated));
});

app.MapGet("/game/{seq}/debug", (int seq) =>
{

    var game = GameDatabase.Instance.Get(seq);
    if (game is null) return Results.NotFound();

    Console.WriteLine("Game at {0} ({1})", game.PlyCount, game.CurrentPlayer);

    foreach (var piece in Enum.GetValues<Piece>())
    {
        Console.WriteLine(piece.ToString());
        Bitboards.Debug(game.CurrentPosition[piece]);
    }

    Console.WriteLine("En passant:");
    Bitboards.Debug(1ul << game.CurrentPosition.EnPassant);

    Console.WriteLine("Evaluation: {0}", Engine.Evaluate(game.CurrentPosition));

    return Results.NoContent();
});

app.Run();