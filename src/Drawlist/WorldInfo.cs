using System.Numerics;

namespace VrmImpl.Drawlist;

public record struct WorldInfo(Matrix4x4 view, Matrix4x4 proj) { }
