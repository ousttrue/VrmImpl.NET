namespace VrmImpl.Drawlist;

public record Mesh(
    VertexInfo Vertices,
    IndexInfo? Indices,
    IReadOnlyList<Primitive> Primitives,
    SkinVertex[]? SkinVertices
) { }
