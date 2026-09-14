namespace VrmImpl.SceneGraph;

public class GltfSkin
{
    public string? name { get; set; }
    public int? inverseBindMatrices { get; set; }
    public int? skeleton { get; set; }
    public required int[] joints { get; set; }
}

public struct Byte4
{
    public byte Y;
    public byte X;
    public byte Z;
    public byte W;
}

public struct UShort4
{
    public ushort X;
    public ushort Y;
    public ushort Z;
    public ushort W;
}
