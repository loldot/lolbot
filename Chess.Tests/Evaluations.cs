namespace Chess.Api;

public class Evaluations
{
    [Test]
    public void New_Game_Should_Have_EqualPosition()
    {
        var newGame = Engine.NewGame();
        var eval = Engine.Evaluate(newGame.CurrentPosition);
        eval.Should().Be(0);
    }

    [Test]
    public void Capture_Black_Pawn_Should_Give_White_Lead()
    {
        var game = Engine.NewGame();

        game = Engine.Move(game, "E2", "E4");
        
        game = Engine.Move(game, "D7", "D5");
        
        game = Engine.Move(game, "E4", "D5");

        var eval = Engine.Evaluate(game.CurrentPosition);
        eval.Should().Be(100);
    }
}