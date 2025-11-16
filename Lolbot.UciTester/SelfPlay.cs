namespace Lolbot.Core;

public class SelfPlay
{
    public string Output { get; set; } = @$"C:\dev\chess-data\{DateTime.Today:yyyy-MM-dd}_selfplay.txt";
    public string WhiteEnginePath { get; set; } = "";
    public string BlackEnginePath { get; set; } = "";
    public int NumberOfGames { get; set; } = 1;
    public int TimePerMoveMs { get; set; } = 1000;
    public int MaxMovesPerGame { get; set; } = 200;


}