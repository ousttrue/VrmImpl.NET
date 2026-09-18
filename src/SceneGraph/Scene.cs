using System.Buffers;
using System.Numerics;
using System.Runtime.InteropServices;
using VrmImpl.Drawlist;
using VrmImpl.SceneGraph.Gltf;

namespace VrmImpl.SceneGraph;

public class Scene(Node root, IReadOnlyList<Node> nodes, string asset)
{
    public string Asset = asset;
    public Node Root = root;
    public List<Node> Nodes = nodes.ToList();
    public readonly List<Animation> Animations = [];
    public int CurrentAnimation = 0;
    private readonly List<Draw> _drawlist = [];

    public Humanoid? Humanoid;

    public static Scene? LoadFilePath(string path)
    {
        var ext = Path.GetExtension(path);
        switch (ext.ToLower())
        {
            case ".gltf":
                return GltfLoader.LoadScene(File.ReadAllBytes(path), path, []);

            case ".glb":
            case ".vrm":
                return LoadGlb(File.ReadAllBytes(path), path);

            case ".bvh":
                return Bvh.BvhScene.LoadScene(path);

            default:
                throw new NotImplementedException($"{ext} unknown file type");
        }
    }

    public static Scene? LoadGlb(byte[] bytes, string path)
    {
        var glb = Glb.FromBytes(bytes);
        return GltfLoader.LoadScene(glb.Json, path, glb.Bin);
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
