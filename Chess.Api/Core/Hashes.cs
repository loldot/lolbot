using Lolbot.Core;

public class Hashes
{
    private static readonly Random random = new Random();
    private static readonly ulong[][] Seeds = new ulong[12][];
    private static readonly ulong CastlingRights;

    static Hashes()
    {
        CastlingRights = (ulong)random.NextInt64();
        for (int i = 0; i < 12; i++)
        {
            Seeds[i] = new ulong[64];
            for (int j = 0; j < 64; j++)
            {
                Seeds[i][j] = (ulong)random.NextInt64();
            }
        }
    }

    public static ulong GetValue(Castle castlingRights) => unchecked((ulong)castlingRights * CastlingRights);

    public static ulong GetValue(Piece piece, byte square) => piece switch
    {
        Piece.WhitePawn => Seeds[0][square],
        Piece.WhiteKnight => Seeds[1][square],
        Piece.WhiteBishop => Seeds[2][square],
        Piece.WhiteRook => Seeds[3][square],
        Piece.WhiteQueen => Seeds[4][square],
        Piece.WhiteKing => Seeds[5][square],
        Piece.BlackPawn => Seeds[6][square],
        Piece.BlackKnight => Seeds[7][square],
        Piece.BlackBishop => Seeds[8][square],
        Piece.BlackRook => Seeds[9][square],
        Piece.BlackQueen => Seeds[10][square],
        Piece.BlackKing => Seeds[11][square],
        _ => 0,
    };
}