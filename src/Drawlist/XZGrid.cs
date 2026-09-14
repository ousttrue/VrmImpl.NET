using System.Numerics;
using System.Runtime.InteropServices;

namespace VrmImpl.Drawlist.Gizmo;

public static class XZGrid
{
    static readonly Vector4 WHITE = new(0.3f, 0.3f, 0.3f, 1.0f);
    static readonly Vector4 RED = new(1, 0.4f, 0.4f, 1);
    static readonly Vector4 BLUE = new(0.4f, 0.4f, 1, 1);

    public static Mesh CreateMesh(int count = 5, int size = 1)
    {
        var n = 2 * count + 1 + 2;
        var vertices = new LineVertex[2 * (n + n)];
        var i = 0;
        for (int z = -count; z <= count; ++z)
        {
            if (z == 0)
            {
                vertices[i++] = new(size * (-Vector3.UnitX * count), WHITE);
                vertices[i++] = new(Vector3.Zero, WHITE);
                vertices[i++] = new(Vector3.Zero, RED);
                vertices[i++] = new(size * (Vector3.UnitX * count), RED);
            }
            else
            {
                vertices[i++] = new(size * (-Vector3.UnitX * count + Vector3.UnitZ * z), WHITE);
                vertices[i++] = new(size * (Vector3.UnitX * count + Vector3.UnitZ * z), WHITE);
            }
        }
        for (int x = -count; x <= count; ++x)
        {
            if (x == 0)
            {
                vertices[i++] = new(size * (-Vector3.UnitZ * count), WHITE);
                vertices[i++] = new(Vector3.Zero, WHITE);
                vertices[i++] = new(Vector3.Zero, BLUE);
                vertices[i++] = new(size * (Vector3.UnitZ * count), BLUE);
            }
            else
            {
                vertices[i++] = new(size * (-Vector3.UnitZ * count + Vector3.UnitX * x), WHITE);
                vertices[i++] = new(size * (Vector3.UnitZ * count + Vector3.UnitX * x), WHITE);
            }
        }

        return new Mesh(
            new(LineVertex.Layout, MemoryMarshal.Cast<LineVertex, byte>(vertices).ToArray()),
            default,
            [new(0, vertices.Length, new(new("line", (Topology.Lines, LineVertex.Layout))))],
            default
        );
    }
}
