using System.Data;
using System.Text.RegularExpressions;

namespace Lolbot.Core;

public class Uci
{
    private string command = null!;
    private Game game = Engine.NewGame();

    public void Run()
    {
        while (true)
        {
            command = Console.ReadLine() ?? "quit";
            // log.WriteLine(command);
            // log.Flush();

            if (command == "quit") break;
            else if (command == "uci") Init();
            else if (command == "isready") IsReady();
            else if (command.StartsWith("position")) game = SetPosition(command);
            else if (command.StartsWith("go")) Go(command);
            else if (command.StartsWith("perft")) Perft(command);
            else if (command.StartsWith("ucinewgame")) Reset();
            else if (command.StartsWith("eval")) Evaluate();
            else Unknown(command);
        }
    }

    private void Evaluate()
    {
        var fen = new FenSerializer().ToFenString(game.CurrentPosition.AsReadOnly());

        Heuristics.StaticEvaluation(game.CurrentPosition, debug: true);

        Console.WriteLine();
        Console.WriteLine(fen);
    }

    private void Reset()
    {
        Engine.Reset();
    }

    private void Perft(string command)
    {
        var tokens = Regex.Split(command, @"\s");
        int depth = 6;

        if (tokens.Length > 1) depth = int.Parse(tokens[1]);

        var start = DateTime.Now;
        var count = Engine.Perft2(new MutablePosition(), depth);
        var ms = (DateTime.Now - start).TotalMilliseconds;
        var mNodesPerSec = count / (1_000 * ms);
        Console.WriteLine($"Total nodes: {count}, Elapsed: {ms:N0}ms, ({mNodesPerSec:N0} mnodes/s)");
    }

    private void Init()
    {
        Console.WriteLine("id name Lolbot 1.0 alpha");
        Console.WriteLine("id author loldot");

        Console.WriteLine("uciok");
    }

    private void IsReady()
    {
        Engine.Init();
        Console.WriteLine("readyok");
    }

    private Game SetPosition(string command)
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

                if (tokens[i].Length == 4)
                {
                    Engine.Move(game, fromSq, toSq);
                }
                else // promotion
                {
                    var promotionPiece = tokens[i][4];
                    Engine.Move(game, fromSq, toSq, promotionPiece);
                }
            }
        }

        return game;
    }

    private void Go(string command)
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

        var (timeleft, increment) = game.CurrentPlayer == Colors.White
            ? (wtime, winc)
            : (btime, binc);

        var clock = new Clock();
        var ct = clock.Start(timeleft, increment);

        var move = Engine.BestMove(game, ct);

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

    private void Unknown(string command)
    {
        Console.WriteLine($"Unknown command: {command}");

        Help();
    }

    private void Help()
    {
        Console.WriteLine("Available commands:");
        Console.WriteLine("  uci - Initialize the engine");
        Console.WriteLine("  isready - Check if the engine is ready");
        Console.WriteLine("  position <fen|startpos> <?moves <moves>> - Set the position from a FEN string");
        Console.WriteLine("  go - Start searching for the best move");
        Console.WriteLine("  perft <depth> - Run perft for the given depth");
        Console.WriteLine("  ucinewgame - Reset the engine for a new game");
        Console.WriteLine("  eval - Evaluate the current position");
        Console.WriteLine("  quit - Exit the engine");
    }

    public static void PrintProgress(SearchProgress progress)
    {
        var nps = (int)(progress.Nodes / progress.Time);
        Console.Write($"info score cp {progress.Eval} ");
        Console.Write($"depth {progress.Depth} ");
        Console.Write($"bm {progress.BestMove} ");
        Console.Write($"nodes {progress.Nodes} ");
        Console.Write($"nps {nps}");
        // pv {string.Join(' ', pv[..])}");
        Console.WriteLine();
    }
}