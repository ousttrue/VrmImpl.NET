using System.Numerics;
using VrmImpl.Drawlist;

namespace VrmImpl.SceneGraph;

public record struct Vertex(Vector3 Position, Vector3 Normal = default, Vector2 TexCoords = default)
{
    public static readonly FloatVertexLayout[] Layout =
    [
        new(0, 3, 32, 0),
        new(1, 3, 32, 12),
        new(2, 2, 32, 24),
    ];
}
