using System.Diagnostics;

namespace Chess.Api.Testing;

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
        _process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = enginePath,
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            }
        };
        if (!File.Exists(enginePath)) throw new FileNotFoundException(enginePath);
        _process.Start();
        _stdin = _process.StandardInput;
        _stdout = _process.StandardOutput;
    }

    public void Initialize()
    {
        Send("uci");
        WaitFor("uciok");
        Send("isready");
        WaitFor("readyok");
    }

    public SearchResult SearchPosition(string fen, int depth)
    {
        Send($"position fen {fen}");
        Send($"go depth {depth}");
        var result = new SearchResult { Fen = fen, Depth = depth, StartTime = DateTime.UtcNow };
        string? line;
        while ((line = _stdout.ReadLine()) != null)
        {
            if (line.StartsWith("info")) ParseInfo(line, result);
            else if (line.StartsWith("bestmove")) { result.BestMove = line.Split(' ')[1]; break; }
        }
        result.EndTime = DateTime.UtcNow;
        return result;
    }

    private static void ParseInfo(string line, SearchResult r)
    {
        var parts = line.Split(' ');
        for (int i = 0; i < parts.Length - 1; i++)
        {
            switch (parts[i])
            {
                case "depth": if (int.TryParse(parts[i + 1], out var d)) r.ActualDepth = Math.Max(r.ActualDepth, d); break;
                case "nodes": if (long.TryParse(parts[i + 1], out var n)) r.Nodes = Math.Max(r.Nodes, n); break;
                case "time": if (int.TryParse(parts[i + 1], out var t)) r.TimeMs = Math.Max(r.TimeMs, t); break;
                case "score":
                    if (i + 2 < parts.Length)
                    {
                        if (parts[i + 1] == "cp" && int.TryParse(parts[i + 2], out var cp)) r.ScoreCp = cp;
                        else if (parts[i + 1] == "mate" && int.TryParse(parts[i + 2], out var mate)) r.ScoreMate = mate;
                    }
                    break;
                case "pv":
                    r.PrincipalVariation = string.Join(' ', parts.Skip(i + 1).TakeWhile(IsMove));
                    break;
            }
        }
    }
    private static bool IsMove(string m) => m.Length is >= 4 and <= 5 && char.IsLetter(m[0]) && char.IsDigit(m[1]) && char.IsLetter(m[2]) && char.IsDigit(m[3]);
    private void Send(string cmd) { if (_enableLogging) Console.WriteLine($">> {cmd}"); _stdin.WriteLine(cmd); _stdin.Flush(); }
    private void WaitFor(string token) { string? l; while ((l = _stdout.ReadLine()) != null) if (l.Contains(token)) break; }
    public void Dispose()
    {
        if (_disposed) return;
        try { _stdin.WriteLine("quit"); _stdin.Flush(); } catch { }
        if (!_process.WaitForExit(3000)) _process.Kill();
        _stdin.Dispose(); _stdout.Dispose(); _process.Dispose();
        _disposed = true;
    }
}

public class SearchResult
{
    public string Fen { get; set; } = string.Empty;
    public int Depth { get; set; }
    public int ActualDepth { get; set; }
    public long Nodes { get; set; }
    public int TimeMs { get; set; }
    public int? ScoreCp { get; set; }
    public int? ScoreMate { get; set; }
    public string BestMove { get; set; } = string.Empty;
    public string PrincipalVariation { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public bool IsCorrectMove { get; set; }
    public double SearchTimeSeconds => (EndTime - StartTime).TotalSeconds;
    public long CalculatedNps => SearchTimeSeconds > 0 ? (long)(Nodes / SearchTimeSeconds) : 0;
}