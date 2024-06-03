using System.Text.RegularExpressions;

namespace Chess.Api;

public sealed partial class PgnSerializer
{
    public async Task<(Game, GameMetadata)> Read(Stream stream)
    {
        using var reader = new StreamReader(stream);
        return await Read(reader);
    }

    public async Task<(Game, GameMetadata)> Read(TextReader reader)
    {
        var metadata = await ReadTagPairs(reader);

        var game = ReadMoves(metadata, reader);

        return (game, metadata);
    }

    private static Game ReadMoves(GameMetadata meta, TextReader reader)
    {
        var position = meta.GetInitialPosition();
        var game = new Game(position, []);

        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            foreach (var token in PgnScanners.NonToken().Split(line))
            {
                if (string.IsNullOrEmpty(token)) continue;
                if (PgnScanners.MoveNumber().IsMatch(token)) continue;
                if (PgnScanners.Comment().IsMatch(token)) continue;
                if (PgnScanners.Result().IsMatch(token)) continue;

                Move move;
                if (token == "O-O" || token == "0-0") move = Move.Castle(game.CurrentPlayer);
                else if (token == "O-O-O" || token == "0-0-0") move = Move.QueenSideCastle(game.CurrentPlayer);
                else
                {
                    var match = PgnScanners.SanToken().Match(token);

                    var piece = match.Groups["piece"].Success
                        ? match.Groups["piece"].ValueSpan
                        : "P";

                    var disambiguation = match.Groups["disambiguation"].ValueSpan;
                    var coords = match.Groups["square"].ValueSpan;

                    var to = Utils.SquareFromCoordinates(coords);
                    var from = Disambiguate(game, to, piece, disambiguation);

                    move = new Move(from, to);
                }

                game = game with { Moves = [.. game.Moves, move] };
            }
        }

        return game;
    }

    private static Square Disambiguate(
        Game game,
        Square to,
        ReadOnlySpan<char> pieceName,
        ReadOnlySpan<char> disambiguation)
    {
        var piece = Utils.FromName(pieceName[0]);
        var legalMoves = game.CurrentPosition.GenerateLegalMoves(game.CurrentPlayer, piece);

        foreach (var move in legalMoves)
        {
            if (Utils.SquareFromIndex(move.ToIndex) == to) 
                return Utils.SquareFromIndex(move.FromIndex);
        }

        return Utils.SquareFromCoordinates("E2");
    }

    private static async Task<GameMetadata> ReadTagPairs(TextReader reader)
    {
        var metadata = new GameMetadata();
        string? line;
        while (!string.IsNullOrWhiteSpace(line = await reader.ReadLineAsync()))
        {
            if (TagPair.CanParse(line))
            {
                var tag = TagPair.Parse(line!);
                metadata.Add(tag.Name, tag.Value);
            }
        }

        return metadata;
    }

    private record TagPair
    {
        public required string Name { get; init; }
        public required string Value { get; init; }

        public static TagPair Parse(string input)
        {
            var match = PgnScanners.TagPair().Match(input);
            return new TagPair
            {
                Name = match.Groups[1].Value,
                Value = match.Groups[2].Value
            };
        }

        public static bool CanParse(string? input)
            => input is not null && PgnScanners.TagPair().IsMatch(input);
    }

    /// <summary>
    /// Lexical scanners for Pgn notation
    /// </summary>
    private static partial class PgnScanners
    {
        [GeneratedRegex(@"^\[(\w+)\s\""(.+)\""\]$")]
        public static partial Regex TagPair();

        [GeneratedRegex(@"\d+\.(\.\.)?")]
        public static partial Regex MoveNumber();

        [GeneratedRegex(@"^;.+$|{.+}")]
        public static partial Regex Comment();

        [GeneratedRegex(@"\*|0-1|1-0|0-0|1/2-1/2")]
        public static partial Regex Result();

        [GeneratedRegex(@"\s+|\{.+\}|^;.+$")]
        public static partial Regex NonToken();

        [GeneratedRegex(@"^(?<piece>[NBRQK]{1})?(?<disambiguation>[a-h]?\d?)?(?<cap>x)?(?<square>[a-h][0-8]).*$")]
        public static partial Regex SanToken();
    }
}