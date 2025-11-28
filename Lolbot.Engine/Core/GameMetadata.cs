
namespace Lolbot.Core;

public class GameMetadata : Dictionary<string, string>
{
    public const string SetupTagName = "Setup";
    public const string WhitePlayerTagName = "White";
    public const string BlackPlayerTagName = "Black";

    internal MutablePosition GetInitialPosition()
    {
        if (ContainsKey(SetupTagName) && this[SetupTagName] == "1")
        {
            throw new NotImplementedException();
        }
        var position = new MutablePosition();
        return (ContainsKey(SetupTagName) && this[SetupTagName] == "1")
            ? throw new NotImplementedException()
            : new MutablePosition();
    }
}