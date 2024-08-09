
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
    else if (command.StartsWith("go")) Go();
    else Unknown(command);
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

void Go()
{
    var move = Engine.Reply(game);

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