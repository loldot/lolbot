using Lolbot.Core;

namespace Lolbot.Nnue;

public class PgnReader
{
    public static async IAsyncEnumerable<(Game, GameMetadata)> ReadGamesAsync(Stream stream)
    {
        using var reader = new StreamReader(stream);
        var pgnSerializer = new PgnSerializer();
        while (true)
        {

            var (game, meta) = await pgnSerializer.ReadSingle(reader);
            if (game == null) yield break;
            yield return (game, meta);
        }
    }
}
