namespace VrmImpl.SceneGraph;

public class GltfAnimationTarget
{
    public int node { get; set; }
    public required string path { get; set; }
}

public class GltfAnimationSampler
{
    public required int input { get; set; }
    public string? interpolation { get; set; }
    public required int output { get; set; }
}

public class GltfAnimationChannel
{
    public required int sampler { get; set; }
    public required GltfAnimationTarget target { get; set; }
}

public class GltfAnimation
{
    public required GltfAnimationSampler[] samplers { get; set; }
    public required GltfAnimationChannel[] channels { get; set; }
}
