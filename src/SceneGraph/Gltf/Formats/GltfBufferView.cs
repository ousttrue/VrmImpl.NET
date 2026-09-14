namespace VrmImpl.SceneGraph;

public class GltfBufferView
{
    public string? name { get; set; }
    public required int buffer { get; set; }
    public int byteOffset { get; set; } = 0;
    public required int byteLength { get; set; }
    public int? byteStride { get; set; }
    public int? target { get; set; }
}
