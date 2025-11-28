using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace Lolbot.Core;

public sealed partial class PgnSerializer
{
    public static async IAsyncEnumerable<(Game, GameMetadata)> ReadMultiple(Stream stream)
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

    public async Task<(Game?, GameMetadata)> ReadSingle(Stream stream)
    {
        var x = MovePatterns.Kings;
        Console.WriteLine(x.Length);

        using var reader = new StreamReader(stream);
        return await ReadSingle(reader);
    }

    StringBuilder sb = new StringBuilder(1024);

    public async Task<(Game?, GameMetadata)> ReadSingle(TextReader reader)
    {
        Game? game = null;
        try
        {
            var metadata = await ReadTagPairs(reader);

            game = ReadMoves(metadata, reader);
            sb.Clear();
            return (game, metadata);
        }
        catch (Exception)
        {
            Console.WriteLine(game?.CurrentPosition.ToDebugString());
            Console.WriteLine(sb);
            throw;
        }
    }

    public static Move ParseMove(Game game, string token)
    {
        Move move;
        token = token.TrimEnd('+', '#');
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
            var promotion = match.Groups["promotion"].Success
                ? match.Groups["promotion"].ValueSpan[1..]
                : ReadOnlySpan<char>.Empty;

            var to = Squares.FromCoordinates(coords);

            Console.WriteLine("Parsing move: {0}", token);

            move = Disambiguate(game, to, piece, disambiguation, promotion) 
                ?? throw new PgnParseException($"Could not disambiguate move {token}");
        }
        return move;
    }

    private Game? ReadMoves(GameMetadata meta, TextReader reader)
    {
        var position = meta.GetInitialPosition();
        var game = new Game(position);

        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            sb.AppendLine(line);
            foreach (var token in PgnScanners.NonToken().Split(line))
            {
                if (string.IsNullOrEmpty(token)) continue;
                if (PgnScanners.MoveNumber().IsMatch(token)) continue;
                if (PgnScanners.Comment().IsMatch(token)) continue;
                if (PgnScanners.Result().IsMatch(token)) return game;

                var move = ParseMove(game, token);

                Engine.Move(game, move);
            }
        }

        return null;
    }

    private static Move? Disambiguate(
        Game game,
        Square to,
        ReadOnlySpan<char> pieceName,
        ReadOnlySpan<char> disambiguation,
        ReadOnlySpan<char> promotion)
    {

        var piece = (game.CurrentPlayer == Colors.White)
            ? Utils.FromName(pieceName[0])
            : Utils.FromName(char.ToLower(pieceName[0]));
        var legalMoves = game.GenerateLegalMoves(piece);

        char? fileAmbiguity = null;
        byte? rankAmbiguity = null;
        char? promotionPiece = promotion.Length > 0 ? promotion[0] : null;

        for (int i = 0; i < disambiguation.Length; i++)
        {
            if (char.IsDigit(disambiguation[i])) rankAmbiguity = (byte)(disambiguation[i] - '0');
            if (char.IsBetween(disambiguation[i], 'a', 'h')) fileAmbiguity = disambiguation[i];
        }

        var disambiguated = legalMoves
            .ToArray()
            .Where(move => move.ToSquare == to)
            .Where(move => fileAmbiguity == null || fileAmbiguity == Squares.GetFile(move.FromSquare))
            .Where(move => rankAmbiguity == null || rankAmbiguity == Squares.GetRank(move.FromSquare))
            .Where(move => promotionPiece == null || move.PromotionPieceType == Utils.GetPieceType(promotionPiece.Value));

        if (disambiguated.Count() != 1) Debugger.Break();

        return disambiguated.SingleOrDefault();
    }

    private static async Task<GameMetadata> ReadTagPairs(TextReader reader)
    {
        var metadata = new GameMetadata();
        string? line;

        // skip empty lines
        while (string.IsNullOrWhiteSpace(line = await reader.ReadLineAsync()) && line != null) ;

        do
        {
            if (TagPair.CanParse(line))
            {
                var tag = TagPair.Parse(line!);
                metadata.Add(tag.Name, tag.Value);
            }
        } while (!string.IsNullOrWhiteSpace(line = await reader.ReadLineAsync()));

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

        [GeneratedRegex(@"^;.+$|{.+?}")]
        public static partial Regex Comment();

        [GeneratedRegex(@"\*|0-1|1-0|0-0|1/2-1/2")]
        public static partial Regex Result();

        [GeneratedRegex(@"\s+|\{.+?\}|^;.+$")]
        public static partial Regex NonToken();

        [GeneratedRegex(@"^(?<piece>[NBRQK]{1})?(?<disambiguation>[a-h]?\d?)?(?<cap>x)?(?<square>[a-h][0-8])(?<promotion>=[NBRQ])?.*$")]
        public static partial Regex SanToken();
    }
}

[Serializable]
internal class PgnParseException : Exception
{
    public PgnParseException()
    {
    }

    public PgnParseException(string? message) : base(message)
    {
    }

    public PgnParseException(string? message, Exception? innerException) : base(message, innerException)
    {
    }
}