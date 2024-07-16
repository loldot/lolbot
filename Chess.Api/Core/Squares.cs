using System.Numerics;
using System.Runtime.CompilerServices;
namespace Lolbot.Core;

///<summary>
/// Provide utility methods for squares (ulong) using 
/// LERF (Little-Endian Rank-File) Mapping.
///</summary>
public static class Squares
{
    public const byte A1 = 0, B1 = 1, C1 = 2, D1 = 3, E1 = 4, F1 = 5, G1 = 6, H1 = 7;
    public const byte A2 = 8, B2 = 9, C2 = 10, D2 = 11, E2 = 12, F2 = 13, G2 = 14, H2 = 15;
    public const byte A3 = 16, B3 = 17, C3 = 18, D3 = 19, E3 = 20, F3 = 21, G3 = 22, H3 = 23;
    public const byte A4 = 24, B4 = 25, C4 = 26, D4 = 27, E4 = 28, F4 = 29, G4 = 30, H4 = 31;
    public const byte A5 = 32, B5 = 33, C5 = 34, D5 = 35, E5 = 36, F5 = 37, G5 = 38, H5 = 39;
    public const byte A6 = 40, B6 = 41, C6 = 42, D6 = 43, E6 = 44, F6 = 45, G6 = 46, H6 = 47;
    public const byte A7 = 48, B7 = 49, C7 = 50, D7 = 51, E7 = 52, F7 = 53, G7 = 54, H7 = 55;
    public const byte A8 = 56, B8 = 57, C8 = 58, D8 = 59, E8 = 60, F8 = 61, G8 = 62, H8 = 63;

    static char[] FileNames = ['a', 'b', 'c', 'd', 'e', 'f', 'g', 'h'];
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte GetRank(Square square)
        => (byte)(1 + (BitOperations.Log2(square) >> 3));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static char GetFile(Square square)
     => FileNames[BitOperations.Log2(square) & 7];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Square FromIndex(ref readonly byte index) => 1ul << index;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Square FromCoordinates(ReadOnlySpan<char> coords)
    {
        byte file = (byte)(char.ToLowerInvariant(coords[0]) - 'a');
        byte rank = (byte)(coords[1] - '1');

        return 1ul << (rank * 8 + file);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte ToIndex(Square square) => (byte)BitOperations.Log2(square);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string ToCoordinate(Square square) => $"{GetFile(square)}{GetRank(square)}";

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte IndexFromCoordinate(string coords)
    {
        byte file = (byte)(char.ToLowerInvariant(coords[0]) - 'a');
        byte rank = (byte)(coords[1] - '1');

        return (byte)(rank * 8 + file);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string? CoordinateFromIndex(byte index) => ToCoordinate(FromIndex(index));
}