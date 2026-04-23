using System.Diagnostics;

namespace Lolbot.Core;

public static class SyzygyTablebase
{
    private static string? _path;
    private static int _maxPieces;
    private static bool _initializedThisSession;

    public static string? Path => _path;
    public static int MaxPieces => _maxPieces;
    public static bool Loaded => _initializedThisSession;
    public static int Largest => 5; // Syzygy supports up to 7 pieces

    public static void Init(string? path, int maxPieces = 5)
    {
        _path = path;
        _maxPieces = maxPieces;

        if (string.IsNullOrEmpty(path))
        {
            _initializedThisSession = false;
            return;
        }

        _initializedThisSession = SyzygyNative.fathom_tb_init(path);
    }

    public static void Free()
    {
        SyzygyNative.fathom_tb_free();
        _initializedThisSession = false;
    }

    public static bool CanProbe(MutablePosition position)
    {
        // Console.WriteLine($"Checking if position can be probed. Occupied: {position.Occupied:X}, Pieces: {System.Numerics.BitOperations.PopCount(position.Occupied)}");
        if (!Loaded) return false;
        var pieces = System.Numerics.BitOperations.PopCount(position.Occupied);
        return pieces <= _maxPieces && pieces <= Largest;
    }

    public static int ProbeWdl(MutablePosition position)
    {
        if (!CanProbe(position)) return 100;

        var white = position.White;
        var black = position.Black;
        var kings = position.WhiteKing | position.BlackKing;
        var queens = position.WhiteQueens | position.BlackQueens;
        var rooks = position.WhiteRooks | position.BlackRooks;
        var bishops = position.WhiteBishops | position.BlackBishops;
        var knights = position.WhiteKnights | position.BlackKnights;
        var pawns = position.WhitePawns | position.BlackPawns;
        
        Debug.Assert(white != 0 || black != 0, "No pieces on the board");
        Debug.Assert(kings != 0, "No kings on the board");
        Debug.Assert(System.Numerics.BitOperations.PopCount(kings) == 2, "There must be exactly 2 kings on the board");


        var ep = position.EnPassant;
        var turn = position.CurrentPlayer == Colors.White;
        // Console.WriteLine($"Probing WDL");
        uint res = SyzygyNative.fathom_tb_probe_wdl(white, black, kings, queens, rooks, bishops, knights, pawns, ep, turn);
        // Console.WriteLine($"Probe result: {res}");
        return (int)res;
    }

    public static int ProbeDtz(MutablePosition position)
    {
        return 0;
    }
}