using System.Text;

namespace VrmImpl.SceneGraph.Gltf;

struct Glb(ArraySegment<byte> json, ArraySegment<byte> bin)
{
    public ArraySegment<byte> Json = json;
    public ArraySegment<byte> Bin = bin;

    public override readonly string ToString()
    {
        return Encoding.UTF8.GetString(Json);
    }

    public static Glb FromBytes(byte[] bytes)
    {
        var r = new ByteReader(bytes);
        if (!r.Read(4).AsSpan().SequenceEqual("glTF"u8))
        {
            throw new FormatException("not glb");
        }
        var version = r.Int32();
        if (version != 2)
        {
            throw new FormatException($"unknown version: {version}");
        }
        var length = r.Int32();
        ArraySegment<byte> json = default;
        ArraySegment<byte> bin = default;
        while (r.Pos < length)
        {
            var chunkLength = r.Int32();
            var chunkType = r.Read(4).AsSpan();
            var data = r.Read(chunkLength);
            if (chunkType.SequenceEqual("JSON"u8))
            {
                json = data;
            }
            else if (chunkType.SequenceEqual("BIN\0"u8))
            {
                bin = data;
            }
        }
        return new Glb(json, bin);
    }
}
