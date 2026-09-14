using System.Runtime.InteropServices;

namespace VrmImpl.SceneGraph;

public record struct BufferRange(int Offset, int Length, ArraySegment<byte> Bin)
{
    public BufferRange SubRange(int offset, int length)
    {
        if (offset + length > Length)
        {
            throw new OverflowException();
        }
        return new BufferRange(Offset + offset, length, Bin);
    }

    public ReadOnlySpan<byte> Slice()
    {
        return Bin.Slice(Offset, Length);
    }

    public ReadOnlySpan<T> Slice<T>()
        where T : unmanaged
    {
        return MemoryMarshal.Cast<byte, T>(Bin.Slice(Offset, Length));
    }
}
