using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Lolbot.Core;

public sealed class UciDriver : IDisposable
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


    public string BestMove { get; private set; } = "";
    public (int Depth, int AvgNodes, int TotalNodes, int AvgNps, double BranchingFactor) SearchStats { get; private set; }
    public bool IsFinished { get; set; }

    public void ClearMove()
    {
        BestMove = string.Empty;
        IsFinished = false;
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
                moveStats.Clear();
                BestMove = e.Data.Trim().Split(' ')[1];
                IsFinished = true;
            }
        }
    }

    private List<Dictionary<string, string>> moveStats = new();
    private void SummarizeStats()
    {
        double branchingFactor = 0;

        int averageNodes = 0, previousNodes = 0, totalNodes = 0;
        int averageNps = 0;
        int totalDepth = 0;

        foreach (var stat in moveStats)
        {
            var nodes = GetInt(stat, "nodes");
            var nps = GetInt(stat, "nps");
            totalDepth = GetInt(stat, "depth");
            if (previousNodes != 0 && nodes != 0)
            {
                branchingFactor += nodes / previousNodes;
            }
            previousNodes = nodes;
            averageNodes += nodes;
            totalNodes += nodes;
            averageNps += nps;
        }
        if (moveStats.Count > 0)
        {
            averageNodes /= moveStats.Count;
            averageNps /= moveStats.Count;
            branchingFactor /= moveStats.Count - 1; // since we start calculating from the second entry
        }
        SearchStats = (totalDepth, averageNodes, totalNodes, averageNps, branchingFactor);
    }

    private static int GetInt(Dictionary<string, string> stat, string key)
    {
        if (stat.TryGetValue(key, out string? value))
        {
            return int.TryParse(value, out var n) ? n : 0;
        }
        return 0;
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
            if (!_engineProcess.Value.WaitForExit(500))
            {
                _engineProcess.Value.Kill();
            }

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