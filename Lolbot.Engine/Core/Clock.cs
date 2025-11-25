using static System.Math;

namespace Lolbot.Core;

public class Clock
{
    private DateTime startTime;
    private int hardLimitMs;
    private int softLimitMs;
    private int timeLeft;

    public CancellationToken Start(int timeleft, int increment)
    {
        this.startTime = DateTime.Now;
        timeLeft = timeleft;
        
        // Time management strategy:
        // - Use about 1/30th of remaining time for this move
        // - Add most of the increment (we'll get it back after the move)
        // - Set hard limit at ~3x the soft limit to allow finishing important searches
        
        softLimitMs = Max(50, timeleft / 30 + increment * 3 / 4);
        hardLimitMs = Min(timeleft - 50, softLimitMs * 3); // Reserve 50ms buffer
        
        // Ensure we don't go negative or too low
        hardLimitMs = Max(100, Min(hardLimitMs, timeleft - 50));

        var timer = new CancellationTokenSource(hardLimitMs);

        return timer.Token;
    }

    public bool CanStart(int depth)
    {
        var elapsed = (DateTime.Now - startTime).TotalMilliseconds;

        // Estimate time needed for next depth
        // Chess search typically has a branching factor around 3-4 at higher depths
        var expectedNodes = 1285 * Exp(0.856 * depth);
        var expectedNpms = 1e3; // nodes per millisecond

        var estimatedTimeMs = expectedNodes / expectedNpms;

        // Only start if we have enough time within soft limit
        // or if we're well within hard limit
        if (elapsed + estimatedTimeMs <= softLimitMs)
        {
            return true;
        }

        if (elapsed + estimatedTimeMs / 2 <= hardLimitMs)
        {
            return true;
        }

        return false;
    }
    
    public int MillisecondsElapsedThisTurn => (int)(DateTime.Now - startTime).TotalMilliseconds;
    public int MillisecondsRemaining =>  Max(0, timeLeft - MillisecondsElapsedThisTurn);
    public int SoftLimit => softLimitMs;
    public int HardLimit => hardLimitMs;
}