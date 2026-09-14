using System.Runtime.InteropServices;
using VrmImpl.Drawlist;

namespace VrmImpl.SceneGraph;

static class CubeMesh
{
    // csharpier-ignore
    static Vertex[] vertices => [
        //X    Y      Z       Normals: 36 vertices
        new(new(-0.5f, -0.5f, -0.5f),new( 0.0f, 0.0f, -1.0f)),
        new(new(0.5f, -0.5f, -0.5f), new(0.0f, 0.0f, -1.0f)),
        new(new(0.5f, 0.5f, -0.5f), new(0.0f, 0.0f, -1.0f)),
        new(new(0.5f, 0.5f, -0.5f), new(0.0f, 0.0f, -1.0f)),
        new(new(-0.5f, 0.5f, -0.5f),new( 0.0f, 0.0f, -1.0f)),
        new(new(-0.5f, -0.5f, -0.5f),new( 0.0f, 0.0f, -1.0f)),
        new(new(-0.5f, -0.5f, 0.5f),new( 0.0f, 0.0f, 1.0f)),
        new(new(0.5f, -0.5f, 0.5f), new(0.0f, 0.0f, 1.0f)),
        new(new(0.5f, 0.5f, 0.5f), new(0.0f, 0.0f, 1.0f)),
        new(new(0.5f, 0.5f, 0.5f), new(0.0f, 0.0f, 1.0f)),
        new(new(-0.5f, 0.5f, 0.5f),new( 0.0f, 0.0f, 1.0f)),
        new(new(-0.5f, -0.5f, 0.5f),new( 0.0f, 0.0f, 1.0f)),
        new(new(-0.5f, 0.5f, 0.5f),new( -1.0f, 0.0f, 0.0f)),
        new(new(-0.5f, 0.5f, -0.5f),new( -1.0f, 0.0f, 0.0f)),
        new(new(-0.5f, -0.5f, -0.5f),new( -1.0f, 0.0f, 0.0f)),
        new(new(-0.5f, -0.5f, -0.5f),new( -1.0f, 0.0f, 0.0f)),
        new(new(-0.5f, -0.5f, 0.5f),new( -1.0f, 0.0f, 0.0f)),
        new(new(-0.5f, 0.5f, 0.5f),new( -1.0f, 0.0f, 0.0f)),
        new(new(0.5f, 0.5f, 0.5f), new(1.0f, 0.0f, 0.0f)),
        new(new(0.5f, 0.5f, -0.5f), new(1.0f, 0.0f, 0.0f)),
        new(new(0.5f, -0.5f, -0.5f), new(1.0f, 0.0f, 0.0f)),
        new(new(0.5f, -0.5f, -0.5f), new(1.0f, 0.0f, 0.0f)),
        new(new(0.5f, -0.5f, 0.5f), new(1.0f, 0.0f, 0.0f)),
        new(new(0.5f, 0.5f, 0.5f), new(1.0f, 0.0f, 0.0f)),
        new(new(-0.5f, -0.5f, -0.5f),new( 0.0f, -1.0f, 0.0f)),
        new(new(0.5f, -0.5f, -0.5f), new(0.0f, -1.0f, 0.0f)),
        new(new(0.5f, -0.5f, 0.5f), new(0.0f, -1.0f, 0.0f)),
        new(new(0.5f, -0.5f, 0.5f), new(0.0f, -1.0f, 0.0f)),
        new(new(-0.5f, -0.5f, 0.5f),new( 0.0f, -1.0f, 0.0f)),
        new(new(-0.5f, -0.5f, -0.5f),new( 0.0f, -1.0f, 0.0f)),
        new(new(-0.5f, 0.5f, -0.5f),new( 0.0f, 1.0f, 0.0f)),
        new(new(0.5f, 0.5f, -0.5f), new(0.0f, 1.0f, 0.0f)),
        new(new(0.5f, 0.5f, 0.5f), new(0.0f, 1.0f, 0.0f)),
        new(new(0.5f, 0.5f, 0.5f), new(0.0f, 1.0f, 0.0f)),
        new(new(-0.5f, 0.5f, 0.5f),new( 0.0f, 1.0f, 0.0f)),
        new(new(-0.5f, 0.5f, -0.5f),new( 0.0f, 1.0f, 0.0f)),
    ];
    static readonly byte[] verticesData = MemoryMarshal.Cast<Vertex, byte>(vertices).ToArray();

    static uint[] indices => Enumerable.Range(0, vertices.Length).Select(x => (uint)x).ToArray();

    static readonly byte[] indicesData = MemoryMarshal.Cast<uint, byte>(indices).ToArray();

    public static Mesh Create(Material material)
    {
        var mesh = new Mesh(
            new(Vertex.Layout, verticesData),
            new((uint)Marshal.SizeOf(indices[0]), indicesData),
            [new Primitive(0, indices.Length, material)],
            default
        );
        return mesh;
    }
}
