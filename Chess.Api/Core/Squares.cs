using System.Numerics;
using System.Runtime.CompilerServices;

///<summary>
/// Provide utility methods for squares (ulong) using 
/// LERF (Little-Endian Rank-File) Mapping.
///</summary>
public static class Squares
{
    static char[] FileNames = ['a', 'b', 'c', 'd', 'e', 'f', 'g', 'h'];
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte GetRank(Square square)
        => (byte)(1 + (BitOperations.Log2(square) >> 3));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static char GetFile(Square square)
     => FileNames[BitOperations.Log2(square) & 7];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Square FromIndex(byte index) => 1ul << index;

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