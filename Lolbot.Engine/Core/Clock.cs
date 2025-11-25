using static System.Math;

namespace Lolbot.Core;

public class Clock
{
    private DateTime startTime;
    private int hardLimitMs;
    private int softLimitMs;
    private int timeLeft;
    private int increment;

    public CancellationToken Start(int timeleft, int increment)
    {
        this.startTime = DateTime.Now;
        this.timeLeft = timeleft;
        this.increment = increment;
        
        // Calculate time allocation
        // Use a fraction of remaining time with consideration for increment
        // This assumes average game length of 40 moves
        int expectedMovesToGo = 40;
        
        // Soft limit: target time per move
        softLimitMs = (timeleft / expectedMovesToGo) + (increment / 2);
        
        // Hard limit: maximum time allowed for this move
        hardLimitMs = Min(
            (timeleft / 10) + increment, // Never use more than 1/10 of remaining time
            softLimitMs * 3 // Allow up to 3x soft limit
        );
        
        // Ensure we don't exceed available time minus safety margin
        int safetyMargin = 50; // 50ms safety buffer
        hardLimitMs = Min(hardLimitMs, timeleft - safetyMargin);
        softLimitMs = Min(softLimitMs, hardLimitMs - 10);
        
        // Ensure positive values
        hardLimitMs = Max(hardLimitMs, 10);
        softLimitMs = Max(softLimitMs, 5);

        var timer = new CancellationTokenSource(hardLimitMs);

        return timer.Token;
    }

    public bool CanStart(int depth)
    {
        var elapsed = (DateTime.Now - startTime).TotalMilliseconds;

        // Exponential growth estimate for node count at next depth
        var expectedNodes = 1285 * Exp(0.856 * depth);
        var expectedNpms = 1e3; // nodes per millisecond

        // Check if we have time to start next depth
        var estimatedTimeForNextDepth = expectedNodes / expectedNpms;
        
        // Don't start next depth if:
        // 1. We've exceeded soft limit
        // 2. Next depth would likely exceed hard limit
        if (elapsed >= softLimitMs)
            return false;
            
        if (elapsed + estimatedTimeForNextDepth >= hardLimitMs * 0.9)
            return false;

        return true;
    }
    
    public int MillisecondsElapsedThisTurn => (int)(DateTime.Now - startTime).TotalMilliseconds;
    public int MillisecondsRemaining => Max(0, timeLeft - MillisecondsElapsedThisTurn);
    public int SoftLimit => softLimitMs;
    public int HardLimit => hardLimitMs;
}
