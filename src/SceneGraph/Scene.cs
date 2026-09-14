using System.Buffers;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text.Json.Nodes;
using VrmImpl.Drawlist;
using UniHumanoid;

namespace VrmImpl.SceneGraph;

public class Scene(Node root, IReadOnlyList<Node> nodes, string asset)
{
    public string Asset = asset;
    public Node Root = root;
    public List<Node> Nodes = nodes.ToList();
    public readonly List<Animation> Animations = [];
    public int CurrentAnimation = 0;
    private readonly List<Draw> _drawlist = [];

    public static Scene? LoadFilePath(string path)
    {
        var ext = Path.GetExtension(path);
        switch (ext.ToLower())
        {
            case ".gltf":
                return LoadGltf(File.ReadAllBytes(path), path, []);

            case ".glb":
            case ".vrm":
                return LoadGlb(File.ReadAllBytes(path), path);

            case ".bvh":
                return LoadBvh(path);

            default:
                throw new NotImplementedException($"{ext} unknown file type");
        }
    }

    public static Scene? LoadGltf(ArraySegment<byte> json, string path, ArraySegment<byte> bin)
    {
        var gltfRoot = JsonNode.Parse(json);
        if (gltfRoot is null)
        {
            Console.Out.WriteLine("no gltf root ?");
            return default;
        }

        var gltf = new GltfLoader(gltfRoot.AsObject(), bin, path);
        var materials = gltf.LoadMaterials();
        var meshes = gltf.LoadMeshes(materials);
        var skins = gltf.LoadSkins();
        var (Root, Nodes) = gltf.LoadHierarchy(meshes, skins);
        var scene = new Scene(Root, Nodes, Path.GetFileName(path));

        if (gltf.Root.TryGetPropertyValue("animations", out var animations))
        {
            if (animations is null)
            {
                throw new Exception();
            }
            foreach (var animationNode in animations.AsArray())
            {
                var animation = gltf.LoadAnimation(animationNode, Nodes);
                scene.Animations.Add(animation);
            }
            scene.CurrentAnimation = new Random().Next(scene.Animations.Count);
        }

        return scene;
    }

    public static Scene? LoadGlb(byte[] bytes, string path)
    {
        var glb = Glb.FromBytes(bytes);
        return LoadGltf(glb.Json, path, glb.Bin);
    }

    public static Scene? LoadBvh(string path)
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

        return scene;
    }

    const float ToRadians = MathF.PI / 180f;

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

    public static Scene CreateFromNodes(string asset, params ReadOnlySpan<Node> nodes)
    {
        var scene = new Scene(new Node(), [], asset);
        foreach (var node in nodes)
        {
            scene.Root.AddChild(node);
            scene.Nodes.Add(node);
        }
        return scene;
    }

    public static Scene LoadCube()
    {
        var lampPosition = new Vector3(1.2f, 1.0f, 2.0f);
        var lamp = new Node
        {
            Mesh = CubeMesh.Create(new(new("shader", (Topology.Triangles, Vertex.Layout)))),
            Scale = new Vector3(0.2f, 0.2f, 0.2f),
            Translation = lampPosition,
        };

        var lightingMaterial = new Material(
            new("lighting", (Topology.Triangles, Vertex.Layout)),
            (shader) =>
            {
                shader.SetUniform("objectColor", new Vector3(1.0f, 0.5f, 0.31f));
                shader.SetUniform("lightColor", Vector3.One);
                shader.SetUniform("lightPos", lampPosition);
            }
        );
        var lighting = new Node
        {
            Mesh = CubeMesh.Create(lightingMaterial),
            Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI * 25f / 180f),
        };

        return CreateFromNodes("Cube", lamp, lighting);
    }

    public void AddDelta(double delta)
    {
        if (CurrentAnimation >= 0 && CurrentAnimation < Animations.Count)
        {
            Animations[CurrentAnimation].UpdateDelta(delta);
        }

        Root.CalcWorld(Matrix4x4.Identity);
    }

    public IReadOnlyList<Draw> MakeDrawList()
    {
        _drawlist.Clear();

        var transformMatrices = Nodes.Select(x => x.WorldTransform).ToArray();

        foreach (var node in Nodes)
        {
            if (node.Mesh is Mesh mesh)
            {
                // morphTarget and boneSkinning
                VertexInfo? meshAnimation = default;

                if (node.Skin is Skin skin)
                {
                    // boneSkinning
                    Matrix4x4.Invert(node.WorldTransform, out var baseMatrix);
                    var data = skin.Deform(
                        baseMatrix,
                        MemoryMarshal.Cast<byte, Vertex>(mesh.Vertices.Data),
                        mesh.SkinVertices ?? throw new Exception(),
                        transformMatrices,
                        (dst, i) =>
                        {
                            dst[i].Position = Vector3.Zero;
                            dst[i].Normal = Vector3.Zero;
                        },
                        (dst, src, i, m, w) =>
                        {
                            dst[i].Position += Vector3.Transform(src[i].Position, m) * w;
                            dst[i].Normal += Vector3.TransformNormal(src[i].Normal, m) * w;
                        }
                    );
                    meshAnimation = new(Vertex.Layout, data);
                }

                _drawlist.Add(new Draw(mesh, node.WorldTransform, meshAnimation));
            }
        }
        return _drawlist;
    }
}
