using System.Numerics;

namespace VrmImpl.Drawlist.Gizmo;

public record struct LineVertex(Vector3 Position, Vector4 Color)
{
    public static readonly FloatVertexLayout[] Layout = [new(0, 3, 28, 0), new(1, 4, 28, 12)];
}
