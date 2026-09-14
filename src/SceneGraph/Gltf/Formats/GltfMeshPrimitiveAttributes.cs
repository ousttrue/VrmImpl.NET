namespace VrmImpl.SceneGraph;

public record GltfMeshPrimitiveAttributes(
    int POSITION,
    int? NORMAL,
    int? TANGENT,
    int? TEXCOORD_0,
    int? COLOR_0,
    int? JOINTS_0,
    int? WEIGHTS_0
)
{ }
