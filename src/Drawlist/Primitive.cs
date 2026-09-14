namespace VrmImpl.Drawlist;

public class Primitive(int DrawOffset, int DrawCount, Material Material)
{
    public readonly int DrawOffset = DrawOffset;
    public readonly int DrawCount = DrawCount;
    public readonly Material Material = Material;
}
