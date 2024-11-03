
using System.Text.RegularExpressions;
using Lolbot.Core;

// FileStream logfile = File.OpenWrite("log.txt");
// StreamWriter log = new(logfile);

string command;
Game game = Engine.NewGame();

while (true)
{
    command = Console.ReadLine() ?? "quit";
    // log.WriteLine(command);
    // log.Flush();

    if (command == "quit") break;
    else if (command == "uci") Uci();
    else if (command == "isready") IsReady();
    else if (command.StartsWith("position")) game = SetPosition(command);
    else if (command.StartsWith("go")) Go(command);
    else if (command.StartsWith("perft")) Perft(command);
    else if (command.StartsWith("ucinewgame")) Reset();
    else Unknown(command);
}

void Reset()
{
    Engine.Reset();
}

void Perft(string command)
{
    var tokens = Regex.Split(command, @"\s");
    int depth = 6;

    if (tokens.Length > 1) depth = int.Parse(tokens[1]);

    var start = DateTime.Now;
    var count = Engine.Perft(game.CurrentPosition, depth);
    var ms = (DateTime.Now - start).TotalMilliseconds;
    var mNodesPerSec = count / (1_000 * ms);
    Console.WriteLine($"Total nodes: {count}, Elapsed: {ms:N0}ms, ({mNodesPerSec:N0} mnodes/s)");
}

void Uci()
{
    Console.WriteLine("id name Lolbot 1.0 alpha");
    Console.WriteLine("id author loldot");

    Console.WriteLine("uciok");
}

void IsReady()
{
    Engine.Init();
    Console.WriteLine("readyok");
}

Game SetPosition(string command)
{
    var tokens = Regex.Split(command, @"\s");

    var game = Engine.NewGame();

    for (int i = 1; i < tokens.Length; i++)
    {
        if (tokens[i] == "startpos" || tokens[i] == "moves") continue;
        else if (tokens[i] == "fen")
        {
            game = Engine.FromPosition(string.Join(' ', tokens[(i + 1)..(i + 6)]));
            i += 6;
        }
        else
        {
            var (fromSq, toSq) = (tokens[i][..2], tokens[i][2..]);
            game = Engine.Move(game, fromSq, toSq);
        }

    }

    return game;
}

void Go(string command)
{
    var tokens = Regex.Split(command, @"\s");

    int wtime = 2_000; int winc = 0;
    int btime = 2_000; int binc = 0;

    for (int i = 1; i < tokens.Length; i++)
    {
        if (tokens[i] == "wtime") wtime = int.Parse(tokens[++i]);
        if (tokens[i] == "btime") btime = int.Parse(tokens[++i]);
        if (tokens[i] == "winc") winc = int.Parse(tokens[++i]);
        if (tokens[i] == "binc") binc = int.Parse(tokens[++i]);
    }

    var (timeleft, increment) = game.CurrentPlayer == Color.White
        ? (wtime, winc)
        : (btime, binc);

    var timer = new CancellationTokenSource(timeleft / 20 + increment / 2);
    var move = Engine.BestMove(game, timer.Token);

    if (move is null)
    {
        Console.Error.WriteLine("No valid moves found");
        return;
    }

    var from = Squares.ToCoordinate(move.Value.FromSquare);
    var to = Squares.ToCoordinate(move.Value.ToSquare);

    if (move.Value.PromotionPiece != Piece.None)
    {
        var promotion = Utils.PieceName(move.Value.PromotionPiece);
        Console.WriteLine($"bestmove {from}{to}{promotion}");
    }

    Console.WriteLine($"bestmove {from}{to}");
}

void Unknown(string command)
{
    Console.WriteLine($"Unknown command: {command}");
}