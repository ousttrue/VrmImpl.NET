using System.Numerics;

namespace VrmImpl.Drawlist;

public record struct Draw(Mesh Mesh, Matrix4x4 Matrix, VertexInfo? MeshAnimation) { }
