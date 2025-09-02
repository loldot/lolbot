using System.Runtime.CompilerServices;
using System.Text;

namespace Lolbot.Core;

public static class Utils
{
    public static Colors Enemy(Colors c) => Colors.White ^ c;
    public static Colors GetColor(Piece piece) => (Colors)((int)piece >> 4);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Piece GetPiece(Colors color, PieceType pieceType) => pieceType == PieceType.None
        ? Piece.None 
        : (Piece)((int)color << 4 | (int)pieceType);
    public static char PieceName(Piece piece) => piece switch
    {
        Piece.WhitePawn => 'P',
        Piece.WhiteKnight => 'N',
        Piece.WhiteBishop => 'B',
        Piece.WhiteRook => 'R',
        Piece.WhiteQueen => 'Q',
        Piece.WhiteKing => 'K',
        Piece.BlackPawn => 'p',
        Piece.BlackKnight => 'n',
        Piece.BlackBishop => 'b',
        Piece.BlackRook => 'r',
        Piece.BlackQueen => 'q',
        Piece.BlackKing => 'k',
        _ => ' ',
    };

    public static Piece FromName(char name) => name switch
    {
        'P' => Piece.WhitePawn,
        'N' => Piece.WhiteKnight,
        'B' => Piece.WhiteBishop,
        'R' => Piece.WhiteRook,
        'Q' => Piece.WhiteQueen,
        'K' => Piece.WhiteKing,
        'p' => Piece.BlackPawn,
        'n' => Piece.BlackKnight,
        'b' => Piece.BlackBishop,
        'r' => Piece.BlackRook,
        'q' => Piece.BlackQueen,
        'k' => Piece.BlackKing,
        _ => Piece.None,
    };

    internal static PieceType GetPieceType(char name) => char.ToLower(name) switch
    {
        'p' => PieceType.Pawn,
        'n' => PieceType.Knight,
        'b' => PieceType.Bishop,
        'r' => PieceType.Rook,
        'q' => PieceType.Queen,
        'k' => PieceType.King,
        _ => PieceType.None
    };

    internal static string CastlingRightsToString(CastlingRights castlingRights)
    {
        StringBuilder sb = new(4);
        if (castlingRights.HasFlag(CastlingRights.WhiteKing)) sb.Append('K');
        if (castlingRights.HasFlag(CastlingRights.WhiteQueen)) sb.Append('Q');
        if (castlingRights.HasFlag(CastlingRights.BlackKing)) sb.Append('k');
        if (castlingRights.HasFlag(CastlingRights.BlackQueen)) sb.Append('q');
        return sb.Length > 0 ? sb.ToString() : "-";
    }
}