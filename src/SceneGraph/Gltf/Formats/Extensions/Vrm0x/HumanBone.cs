namespace VrmImpl.SceneGraph.Gltf.Extensions.Vrm0x;

public class Vrm0xHumanBone
{
    public required int node { get; set; }
    public required string bone { get; set; }

    public override string ToString()
    {
        return $"{bone}";
    }
}

public class Vrm0xHumanoid
{
    public required Vrm0xHumanBone[] humanBones { get; set; }
}
