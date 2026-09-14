namespace VrmImpl.SceneGraph;

class ByteReader(byte[] bytes)
{
    private readonly byte[] bytes = bytes;
    private int pos = 0;
    public int Pos => pos;

    public bool IsEnd => pos >= bytes.Length;

    public ArraySegment<byte> Read(int size)
    {
        if (pos + size > bytes.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(size));
        }
        var read = new ArraySegment<byte>(bytes, pos, size);
        pos += size;
        return read;
    }

    public int Int32()
    {
        var bytes = Read(4);
        if (!BitConverter.IsLittleEndian)
        {
            throw new NotImplementedException("for bigendian not implemented");
        }
        return BitConverter.ToInt32(bytes);
    }
}
