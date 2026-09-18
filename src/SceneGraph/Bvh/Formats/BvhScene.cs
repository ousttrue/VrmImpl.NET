using System.Numerics;
using VrmImpl.Drawlist;

namespace VrmImpl.SceneGraph.Bvh;

public static class BvhScene
{
    const float ToRadians = MathF.PI / 180f;

    private static Node BuildBvhNode(BvhNode bvhNode, List<Node> nodes)
    {
        var node = new Node { Name = bvhNode.Name, Translation = bvhNode.Offset };
        nodes.Add(node);
        foreach (var bvhChild in bvhNode.Children)
        {
            var child = BuildBvhNode(bvhChild, nodes);
            node.AddChild(child);
        }
        return node;
    }

    private static Animation CopyBvhAnimationCurve(
        IReadOnlyList<Node> nodes,
        Bvh bvh,
        BvhNode[] bvhNodes,
        float scale
    )
    {
        var animation = new Animation();

        var input = Enumerable
            .Range(0, bvh.FrameCount)
            .Select(x => bvh.FrameTime.Milliseconds * x * 0.001f)
            .ToArray();

        var channelIndex = 0;
        for (int i = 0; i < nodes.Count; ++i)
        {
            var node = nodes[i];
            var bvhNode = bvhNodes[i];
            var nodeAnimation = new NodeAnimation(node);
            animation.NodeAnimations.Add(nodeAnimation);
            for (int j = 0; j < bvhNode.Channels.Length; )
            {
                ReadOnlySpan<BvhChannel> channels = bvhNode.Channels.AsSpan(j, 3);
                j += 3;
                if (
                    channels.SequenceEqual([
                        BvhChannel.Xposition,
                        BvhChannel.Yposition,
                        BvhChannel.Zposition,
                    ])
                )
                {
                    var output = new Vector3[bvh.FrameCount];
                    for (int k = 0; k < bvh.FrameCount; ++k)
                    {
                        output[k] =
                            new Vector3(
                                bvh.Channels[channelIndex].Keys[k],
                                bvh.Channels[channelIndex + 1].Keys[k],
                                bvh.Channels[channelIndex + 2].Keys[k]
                            ) * scale;
                    }
                    channelIndex += 3;
                    nodeAnimation.T = new Vector3Curve(input, output);
                }
                else if (
                    channels.SequenceEqual([
                        BvhChannel.Zrotation,
                        BvhChannel.Xrotation,
                        BvhChannel.Yrotation,
                    ])
                )
                {
                    var output = new Quaternion[bvh.FrameCount];
                    for (int k = 0; k < bvh.FrameCount; ++k)
                    {
                        // output[k] = Quaternion.CreateFromYawPitchRoll(
                        //     ToRadians * bvh.Channels[channelIndex].Keys[k],
                        //     ToRadians * bvh.Channels[channelIndex + 1].Keys[k],
                        //     ToRadians * bvh.Channels[channelIndex + 2].Keys[k]
                        // );
                        output[k] =
                            Quaternion.CreateFromAxisAngle(
                                Vector3.UnitZ,
                                ToRadians * bvh.Channels[channelIndex].Keys[k]
                            )
                            * Quaternion.CreateFromAxisAngle(
                                Vector3.UnitX,
                                ToRadians * bvh.Channels[channelIndex + 1].Keys[k]
                            )
                            * Quaternion.CreateFromAxisAngle(
                                Vector3.UnitY,
                                ToRadians * bvh.Channels[channelIndex + 2].Keys[k]
                            );
                    }
                    channelIndex += 3;
                    nodeAnimation.R = new QuaternionCurve(input, output);
                }
                else
                {
                    throw new NotImplementedException();
                }
            }
        }

        animation.CalcDuration();

        return animation;
    }

    public static Scene? LoadScene(string path)
    {
        var bvh = Bvh.Parse(File.ReadAllText(path));
        if (bvh is null)
        {
            return default;
        }

        List<Node> nodes = [];
        var root = BuildBvhNode(bvh.Root, nodes);
        root.CalcWorld(Matrix4x4.Identity);

        var minY = float.PositiveInfinity;
        var maxY = float.NegativeInfinity;
        foreach (var node in nodes)
        {
            minY = MathF.Min(minY, node.WorldTransform.Translation.Y);
            maxY = MathF.Max(maxY, node.WorldTransform.Translation.Y);
        }
        // minY to 0
        root.Translation.Y -= minY;
        // scale
        var targetHeight = 1.6f;
        var scale = targetHeight / (maxY - minY);
        foreach (var node in nodes)
        {
            node.Translation *= scale;
        }
        root.CalcWorld(Matrix4x4.Identity);

        root.Skin = new Skin(nodes.Select((x, i) => i).ToArray(), []);
        var scene = new Scene(root, nodes, Path.GetFileName(path));

        // animation
        var bvhNodes = bvh.Root.Traverse().ToArray();
        var animation = CopyBvhAnimationCurve(nodes, bvh, bvhNodes, scale);
        scene.Animations.Add(animation);

        foreach (var node in nodes)
        {
            if (TryGetHumanBone(node, out var humanBone))
            {
                node.HumanBone = humanBone;
            }
        }

        return scene;
    }

    static readonly Dictionary<string, HumanBoneType> HumanBonePreset = new()
    {
        { "Hips", HumanBoneType.Hips },
        { "Spine", HumanBoneType.Spine },
        { "Spine1", HumanBoneType.Chest },
        { "Neck", HumanBoneType.Neck },
        { "Head", HumanBoneType.Head },
        { "LeftShoulder", HumanBoneType.LeftShoulder },
        { "LeftArm", HumanBoneType.LeftUpperArm },
        { "LeftForeArm", HumanBoneType.LeftLowerArm },
        { "LeftHand", HumanBoneType.LeftHand },
        { "RightShoulder", HumanBoneType.RightShoulder },
        { "RightArm", HumanBoneType.RightUpperArm },
        { "RightForeArm", HumanBoneType.RightLowerArm },
        { "RightHand", HumanBoneType.RightHand },
        { "LeftUpLeg", HumanBoneType.LeftUpperLeg },
        { "LeftLeg", HumanBoneType.LeftLowerLeg },
        { "LeftFoot", HumanBoneType.LeftFoot },
        { "LeftToeBase", HumanBoneType.LeftToes },
        { "RightUpLeg", HumanBoneType.RightUpperLeg },
        { "RightLeg", HumanBoneType.RightLowerLeg },
        { "RightFoot", HumanBoneType.RightFoot },
        { "RightToeBase", HumanBoneType.RightToes },
    };

    static bool TryGetHumanBone(Node node, out HumanBoneType humanBone)
    {
        if (HumanBonePreset.TryGetValue(node.Name, out humanBone))
        {
            return true;
        }
        Console.Error.WriteLine($"bvh: {node.Name}");
        return false;
    }
}
