using static System.Math;

namespace Lolbot.Core;

public class Clock
{
    private DateTime startTime;
    private int hardLimitMs;
    private int timeLeft;

    public CancellationToken Start(int timeleft, int increment)
    {
        this.startTime = DateTime.Now;
        timeLeft = timeleft;
        hardLimitMs = timeleft / 24 + increment / 2;

        var timer = new CancellationTokenSource(hardLimitMs);

        return timer.Token;
    }

    public bool CanStart(int depth)
    {
        var elapsed = (DateTime.Now - startTime).TotalMilliseconds;

        var expectedNodes = 1285 * Exp(0.856 * depth);
        var expectedNpms = 1e3;

        return elapsed + (expectedNodes / expectedNpms) <= hardLimitMs;
    }
    public int MillisecondsElapsedThisTurn => (int)(DateTime.Now - startTime).TotalMilliseconds;
    public int MillisecondsRemaining =>  Max(0, timeLeft - MillisecondsElapsedThisTurn);
}