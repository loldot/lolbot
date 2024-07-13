using System.Numerics;

namespace Lolbot.Core;

// public readonly struct Move2
{
    private readonly int value;

    public readonly  byte FromIndex => (byte)(value & 0x3f);
    public readonly byte ToIndex => (byte)((value >> 6) & 0x3f);
    public readonly byte CaptureIndex => (byte)((value >> 12) & 0x3f);
    public readonly bool Color => ((value >> 18) & 1) == 0;
    public readonly byte FromPiece => (byte)((value >> 19) & 7);
    public readonly byte CapturePiece => (byte)((value >> 22) & 7);
    public readonly byte PromotionPiece => (byte)((value >> 25) & 7);

    public Move2(string from, string to) : this(
            Squares.IndexFromCoordinate(from),
            Squares.IndexFromCoordinate(to)
        )
    { }

    public Move2(string from, string to, string captureSquare, char capturePiece) : this(
        Squares.IndexFromCoordinate(from),
        Squares.IndexFromCoordinate(to),
        Squares.IndexFromCoordinate(captureSquare),
        Utils.FromName(capturePiece)
    )
    { }

    public Move2(byte fromIndex, byte toIndex)
    : this(fromIndex, toIndex, 0, Piece.None, Piece.None) { }

    public Move2(byte fromIndex, byte toIndex, byte captureIndex, Piece capturePiece)
    : this(fromIndex, toIndex, captureIndex, capturePiece, Piece.None) { }


    public Move2(
        byte fromIndex,
        byte toIndex,
        byte captureIndex,
        Piece capturePiece,
        Piece promotionPiece)
    {
        value = fromIndex
            | (toIndex << 6)
            | (captureIndex << 12);
            | 
     
        CapturePiece = capturePiece;
        PromotionPiece = promotionPiece;
    }

}