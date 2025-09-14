using System.Diagnostics;
using System.Text;

namespace Lolbot.UciTester;

public class UciEngine : IDisposable
{
    private readonly Process _process;
    private readonly StreamWriter _stdin;
    private readonly StreamReader _stdout;
    private readonly bool _enableLogging;
    private bool _disposed = false;

    public UciEngine(string enginePath, bool enableLogging = false)
    {
        _enableLogging = enableLogging;
        
        if (_enableLogging)
        {
            Console.WriteLine($"=== UCI Session Started at {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC ===");
            Console.WriteLine($"Engine: {enginePath}");
            Console.WriteLine();
        }

        _process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = enginePath,
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = false
            }
        };

        _process.Start();
        _stdin = _process.StandardInput;
        _stdout = _process.StandardOutput;
    }

    public void Initialize()
    {
        SendCommand("uci");
        WaitForResponse("uciok");
        SendCommand("isready");
        WaitForResponse("readyok");
    }

    public SearchResult SearchPosition(string fen, int depth, CancellationToken cancellationToken = default)
    {
        SendCommand($"position fen {fen}");
        SendCommand($"go wtime 50000 btime 50000 winc 500 binc 500");

        // SendCommand($"go depth {depth}");

        var result = new SearchResult
        {
            Fen = fen,
            Depth = depth,
            StartTime = DateTime.UtcNow
        };

        string? line;
        while ((line = _stdout.ReadLine()) != null)
        {
            LogMessage($"<< {line}");
            if (line.StartsWith("info"))
            {
                ParseInfoLine(line, result);
            }
            else if (line.StartsWith("bestmove"))
            {
                result.BestMove = ExtractBestMove(line);
                result.EndTime = DateTime.UtcNow;
                break;
            }
        }

        return result;
    }

    private static void ParseInfoLine(string line, SearchResult result)
    {
        var parts = line.Split(' ');
        for (int i = 0; i < parts.Length - 1; i++)
        {
            switch (parts[i])
            {
                case "depth":
                    if (int.TryParse(parts[i + 1], out int depth))
                        result.ActualDepth = Math.Max(result.ActualDepth, depth);
                    break;
                case "nodes":
                    if (long.TryParse(parts[i + 1], out long nodes))
                        result.Nodes = Math.Max(result.Nodes, nodes);
                    break;
                case "nps":
                    // We'll calculate NPS ourselves from nodes and measured time
                    // Don't trust the engine's reported NPS
                    break;
                case "time":
                    if (int.TryParse(parts[i + 1], out int time))
                        result.TimeMs = Math.Max(result.TimeMs, time);
                    break;
                case "score":
                    if (i + 2 < parts.Length)
                    {
                        if (parts[i + 1] == "cp" && int.TryParse(parts[i + 2], out int cp))
                        {
                            result.ScoreCp = cp;
                        }
                        else if (parts[i + 1] == "mate" && int.TryParse(parts[i + 2], out int mate))
                        {
                            result.ScoreMate = mate;
                        }
                    }
                    break;
                case "pv":
                    // Principal variation starts after "pv"
                    var pvMoves = new List<string>();
                    for (int j = i + 1; j < parts.Length; j++)
                    {
                        if (IsValidMove(parts[j]))
                            pvMoves.Add(parts[j]);
                        else
                            break;
                    }
                    result.PrincipalVariation = string.Join(" ", pvMoves);
                    break;
            }
        }
    }

    private static bool IsValidMove(string move)
    {
        // Basic UCI move format validation (e.g., e2e4, a7a8q)
        return move.Length >= 4 && 
               move.Length <= 5 &&
               char.IsLetter(move[0]) && 
               char.IsDigit(move[1]) &&
               char.IsLetter(move[2]) && 
               char.IsDigit(move[3]);
    }

    private static string ExtractBestMove(string line)
    {
        var parts = line.Split(' ');
        return parts.Length >= 2 ? parts[1] : "";
    }

    private void SendCommand(string command)
    {
        LogMessage($">> {command}");
        _stdin.WriteLine(command);
        _stdin.Flush();
    }

    private void WaitForResponse(string expectedResponse)
    {
        string? line;
        while ((line = _stdout.ReadLine()) != null)
        {
            LogMessage($"<< {line}");
            if (line.Contains(expectedResponse))
                break;
        }
    }

    private void LogMessage(string message)
    {
        if (_enableLogging)
        {
            Console.WriteLine($"{DateTime.UtcNow:HH:mm:ss.fff} {message}");
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _stdin?.WriteLine("quit");
            LogMessage(">> quit");
            
            if (_enableLogging)
            {
                Console.WriteLine($"=== UCI Session Ended at {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC ===");
                Console.WriteLine();
            }
            
            _stdin?.Dispose();
            _stdout?.Dispose();
            
            if (!_process.WaitForExit(5000))
            {
                _process.Kill();
            }
            
            _process?.Dispose();
            _disposed = true;
        }
    }
}

public class SearchResult
{
    public string Fen { get; set; } = "";
    public int Depth { get; set; }
    public int ActualDepth { get; set; }
    public long Nodes { get; set; }
    public int TimeMs { get; set; }
    public int? ScoreCp { get; set; }
    public int? ScoreMate { get; set; }
    public string BestMove { get; set; } = "";
    public string PrincipalVariation { get; set; } = "";
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public bool IsCorrectMove { get; set; }
    
    public double SearchTimeSeconds => (EndTime - StartTime).TotalSeconds;
    
    // Calculate NPS from our measured time and nodes, not from engine
    public long CalculatedNps => SearchTimeSeconds > 0 ? (long)(Nodes / SearchTimeSeconds) : 0;
}