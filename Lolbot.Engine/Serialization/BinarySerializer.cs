using System.Runtime.InteropServices;

namespace Lolbot.Core;

public class BinarySerializer
{
    public static void WritePosition(Stream stream, MutablePosition position, short eval, float wdl)
    {
        int size = MutablePosition.BinarySize + sizeof(short) + sizeof(float);
        
        Span<byte> span = stackalloc byte[size];
        int written = position.CopyTo(span);

        MemoryMarshal.Write(span[written..], in eval);
        MemoryMarshal.Write(span[(written + sizeof(short))..], in wdl);
        
        stream.Write(span);
    }

}