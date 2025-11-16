using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Lolbot.Core;

public class UciDriver : IDisposable
{
    private Lazy<Process> _engineProcess;
    public UciDriver(string enginePath)
    {
        _engineProcess = new(() =>
        {
            var processStartInfo = new ProcessStartInfo
            {
                FileName = enginePath,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            var process = new Process
            {
                StartInfo = processStartInfo,
            };
            process.OutputDataReceived += HandleProcOutput;
            process.Start();
            process.BeginOutputReadLine();

            return process;
        });
    }

    private void HandleProcOutput(object sender, DataReceivedEventArgs e)
    {
        if (!string.IsNullOrEmpty(e.Data))
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write($"[Engine Out] ");
            Console.ResetColor();
            Console.WriteLine($"{e.Data}");

            if (e.Data.StartsWith("info score"))
            {
                AddStats(e.Data);
            }
            else if (e.Data.StartsWith("bestmove"))
            {
                SummarizeStats();
            }

        }
    }
    private List<Dictionary<string, string>> moveStats = new();
    private void SummarizeStats()
    {
        double branchingFactor = 0;

        int averageNodes = 0, previousNodes = 0;
        int averageNps = 0;
        int totalDepth = 0;

        // Implementation for summarizing stats from outputBuffer
        foreach (var stat in moveStats)
        {
            var nodes = stat.ContainsKey("nodes") ? int.TryParse(stat["nodes"], out var n) ? n : 0 : 0;
            var nps = stat.ContainsKey("nps") ? int.TryParse(stat["nps"], out var p) ? p : 0 : 0;
            totalDepth = stat.ContainsKey("depth") ? int.TryParse(stat["depth"], out var d) ? d : 0 : 0;
            if (previousNodes != 0 && nodes != 0)
            {
                branchingFactor += nodes / previousNodes;
            }
            previousNodes = nodes;
            averageNodes += nodes;
            averageNps += nps;
        }
        if (moveStats.Count > 0)
        {
            averageNodes /= moveStats.Count;
            averageNps /= moveStats.Count;
            branchingFactor /= moveStats.Count - 1; // since we start calculating from the second entry
        }

        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"[Engine Stats] Average Nodes: {averageNodes:N0}");
        Console.WriteLine($"[Engine Stats] Branching Factor: {branchingFactor:N3}");
        Console.WriteLine($"[Engine Stats] Average NPS: {averageNps:N0}");
        Console.WriteLine($"[Engine Stats] Total Depth: {totalDepth:N0}");
        Console.ResetColor();

    }

    private void AddStats(string line)
    {
        var parts = Regex.Split(line, @"\s+");
        var stats = new Dictionary<string, string>();
        for (int i = 0; i < parts.Length; i += 2)
        {
            if (i + 1 < parts.Length)
            {
                stats[parts[i]] = parts[i + 1];
            }
        }
        moveStats.Add(stats);
    }


    public void Uci() => SendCommand("uci");
    public void IsReady() => SendCommand("isready");
    public void SetPosition(string fen)
    {
        SendCommand($"position fen {fen}");
        SendCommand("isready");
    }
    public void Go(GoOptions value)
    {
        SendCommand($"go {value}");
    }
    // public void SetPosition(Game game)
    // {
    //     // Implementation for sending a game to the chess engine
    // }

    public void SendCommand(string command)
    {
        Console.WriteLine($"[Engine In] {command}");
        _engineProcess.Value.StandardInput.WriteLine(command);
        _engineProcess.Value.StandardInput.Flush();
    }

    public void Dispose()
    {
        if (_engineProcess.IsValueCreated)
        {
            SendCommand("quit");
            _engineProcess.Value.StandardInput.Flush();
            Thread.Sleep(500); // Give the engine some time to exit gracefully
            _engineProcess.Value.Kill();
            _engineProcess.Value.WaitForExit();
            _engineProcess.Value.Dispose();
        }
    }

    public class GoOptions
    {
        public int WhiteTime { get; set; }
        public int BlackTime { get; set; }
        public int WhiteInc { get; set; }
        public int BlackInc { get; set; }

        override public string ToString()
        {
            return $"wtime {WhiteTime} btime {BlackTime} winc {WhiteInc} binc {BlackInc}";
        }
    }
}