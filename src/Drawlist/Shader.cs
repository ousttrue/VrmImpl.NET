namespace VrmImpl.Drawlist;

public enum Topology
{
    Triangles,
    Lines,
}

public record struct Shader(
    string ShaderName,
    (Topology, FloatVertexLayout[]) VertexInput,
    (byte[] vs, byte[] fs)? Spv = default
) { }
