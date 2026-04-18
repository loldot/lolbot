namespace Lolbot.Core;

public static class Arbiter
{
    public static bool Decide(Game game, out float result)
    {
        var position = game.CurrentPosition;

        if (game.IsCheckMate())
        {
            result = position.CurrentPlayer == Colors.White ? 0 : 1;
            return true;
        }
        
        if (game.IsStaleMate())
        {
            result = 0.5f;
            return true;
        }

        if (game.RepetitionTable.IsDraw(position.Hash))
        {
            result = 0.5f;
            return true;
        }

        if (SyzygyTablebase.CanProbe(position))
        {
            var wdl = SyzygyTablebase.ProbeWdl(position);
            result = wdl switch
            {
                0 => 1, // White wins
                1 => 0.5f, // Draw
                2 => 0, // Black wins
                _ => 100
            };
            return result < 100;

        }

        result = 100;
        return false;
    }
}