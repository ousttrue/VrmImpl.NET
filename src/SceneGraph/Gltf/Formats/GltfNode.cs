namespace VrmImpl.SceneGraph.Gltf;

class GltfNode
{
    public string? name { get; set; }
    public int[]? children { get; set; }
    public float[]? translation { get; set; }
    public float[]? rotation { get; set; }
    public float[]? scale { get; set; }
    public float[]? matrix { get; set; }
    public int? mesh { get; set; }
    public int? skin { get; set; }
}
