using System.Runtime.Intrinsics;
using Lolbot.Api;
using Lolbot.Core;
using Chess.Api.Services;
using Chess.Api.Testing;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

// --- Test results DB migration (one-time) ---
// If an older test_results.db exists one level above (legacy location from UciTester),
// copy it into the API content root so the API owns the database moving forward.
try
{
    var contentRoot = builder.Environment.ContentRootPath;
    var legacyPath = Path.GetFullPath(Path.Combine(contentRoot, "..", "test_results.db"));
    var targetPath = Path.Combine(contentRoot, "test_results.db");
    if (!File.Exists(targetPath) && File.Exists(legacyPath))
    {
        File.Copy(legacyPath, targetPath);
        Console.WriteLine($"[TestResults] Migrated database from '{legacyPath}' to '{targetPath}'.");
    }
}
catch (Exception ex)
{
    Console.WriteLine($"[TestResults] Database migration check failed: {ex.Message}");
}

builder.Services.AddSignalR();
builder.Services.AddCors();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Configure path to test results database (configurable via appsettings or env)
var testResultsDbPath = builder.Configuration.GetValue<string>("TestResults:DatabasePath")
                        ?? Path.Combine(builder.Environment.ContentRootPath, "test_results.db");

builder.Services.AddSingleton(sp => new TestResultsService(testResultsDbPath));
builder.Services.AddSingleton(sp => new TestRunService(testResultsDbPath, sp.GetRequiredService<ILogger<TestRunService>>()));

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

// await GameDatabase.Instance.Seed();

app.MapHub<GameHub>("/game/realtime");

// Test results endpoints
app.MapGet("/api/tests/engines", (TestResultsService svc) => Results.Ok(svc.GetEngineSummaries()))
   .WithName("GetEngineTestSummaries").WithOpenApi();

app.MapGet("/api/tests/positions/{engineId}", (string engineId, TestResultsService svc) =>
{
    var enginePath = Uri.UnescapeDataString(engineId);
    var positions = svc.GetPositionResults(enginePath);
    return Results.Ok(positions);
}).WithName("GetEnginePositionResults").WithOpenApi();

// Run management
app.MapGet("/api/tests/runs", (TestRunService svc) => Results.Ok(svc.GetRuns()))
   .WithName("GetTestRuns").WithOpenApi();

app.MapPost("/api/tests/runs", async ([FromBody] StartRunRequest req, TestRunService svc) =>
{
    var categories = req.Categories?.Length > 0 ? req.Categories : new[] { "CCC" };
    var response = await svc.StartRunAsync(req.CommitHash, req.EnginePath, req.Depth, categories!);
    return Results.Accepted($"/api/tests/runs/{response.Id}", response);
}).WithName("StartTestRun").WithOpenApi();

app.MapPost("/game/new", ([FromBody] string? fen) =>
{
    var (seq, game) = string.IsNullOrEmpty(fen)
        ? GameDatabase.Instance.Create()
        : GameDatabase.Instance.Create(fen);

    return Results.Ok(new { seq, game = new ApiGame(game) });
});

app.MapGet("/game/{seq}/legal-moves/", (int seq) =>
{
    var game = GameDatabase.Instance.Get(seq);
    if (game is null) return Results.NotFound();

    var legalMoves = game.CurrentPosition.GenerateLegalMoves();
    var moves = new string[legalMoves.Length][];
    for (int i = 0; i < legalMoves.Length; i++)
    {
        var x = legalMoves[i];
        moves[i] = [Squares.ToCoordinate(x.FromSquare)!, Squares.ToCoordinate(x.ToSquare)!];
    }

    return Results.Ok(moves);
});

app.MapGet("/game/{seq}/legal-moves/{square}/{piece}", (int seq, string square, string piece) =>
{
    var game = GameDatabase.Instance.Get(seq);
    if (game is null) return Results.NotFound();

    var fromIndex = Squares.IndexFromCoordinate(square);
    var legalMoves = game.CurrentPosition.GenerateLegalMoves();
    var moves = new List<string>();
    for (int i = 0; i < legalMoves.Length; i++)
    {
        var x = legalMoves[i];

        if (fromIndex != x.FromIndex) continue;

        moves.Add(Squares.ToCoordinate(x.FromSquare)!);
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

    Engine.Move(game, movedata[0], movedata[1]);

    GameDatabase.Instance.Update(seq, game);
    return Results.Ok(new ApiGame(game));
});

app.MapGet("/game/{seq}/bitboard/{name}", (int seq, char name) =>
{

    var game = GameDatabase.Instance.Get(seq);
    if (game is null) return Results.NotFound();

    ulong bb;
    if (name == 'x')
    {
        bb = game.CurrentPosition.Checkmask;
        Bitboards.Debug(bb);
    }
    else if (name == 'i')
    {
        bb = Bitboards.Create((Vector256<ulong>)game.CurrentPosition.Pinmasks);
        Bitboards.Debug(bb);

    }
    else if (name == 'o')
    {
        bb = game.CurrentPosition.Occupied;
        Bitboards.Debug(bb);
    }
    else if (name == 'w')
    {
        bb = game.CurrentPosition.White;
        Bitboards.Debug(bb);
    }
    else if (name == 'l')
    {
        bb = game.CurrentPosition.Black;
        Bitboards.Debug(bb);
    }
    else if (name == 'e')
    {
        bb = game.CurrentPosition.Empty;
        Bitboards.Debug(bb);
    }
    else
    {
        var piece = Utils.FromName(name);
        bb = game.CurrentPosition[piece];
    }


    return Results.Ok(Bitboards.ToCoordinates(bb));
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

    // Console.WriteLine("Evaluation: {0}", Engine.Evaluate(game.CurrentPosition));

    return Results.NoContent();
});

app.Run();

public record StartRunRequest(string CommitHash, string EnginePath, int Depth = 12, string[]? Categories = null);