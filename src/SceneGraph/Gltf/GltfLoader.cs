using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;
using VrmImpl.Drawlist;

namespace VrmImpl.SceneGraph;

public record GltfLoader(JsonObject Root, ArraySegment<byte> Bin, string FilePath)
{
    private readonly Dictionary<string, byte[]> _binMap = [];

    public JsonArray GetRootArray(string name)
    {
        if (!Root.TryGetPropertyValue(name, out JsonNode? node))
        {
            throw new Exception($"no {name} property");
        }
        if (node is null)
        {
            throw new Exception($"{name} is null");
        }
        return node.AsArray();
    }

    const string BASE64_PREFIX = "data:application/octet-stream;base64,";

    ArraySegment<byte> GetOrReadUri(string? _uri)
    {
        if (_uri is string uri)
        {
            if (_binMap.TryGetValue(uri, out var bin))
            {
                return bin;
            }
            else if (uri.StartsWith("data:"))
            {
                if (uri.StartsWith(BASE64_PREFIX))
                {
                    bin = Convert.FromBase64String(uri.Substring(BASE64_PREFIX.Length));
                    _binMap.Add(uri, bin);
                    return bin;
                }
                else
                {
                    throw new NotImplementedException();
                }
            }
            else
            {
                bin = File.ReadAllBytes(Path.Join(Path.GetDirectoryName(FilePath), uri));
                _binMap.Add(uri, bin);
                return bin;
            }
        }
        else
        {
            return Bin;
        }
    }

    public GltfBuffer LoadBuffer(int bufferIndex)
    {
        var buffers = GetRootArray("buffers");
        var buffer = buffers[bufferIndex] ?? throw new Exception($"buffers[{bufferIndex}] is null");
        return JsonSerializer.Deserialize<GltfBuffer>(buffer) ?? throw new Exception();
    }

    public GltfBufferView LoadBufferView(int bufferViewIndex)
    {
        var bufferViews = GetRootArray("bufferViews");
        var bufferView =
            bufferViews[bufferViewIndex]
            ?? throw new Exception($"bufferViews[{bufferViewIndex}] is null");
        return JsonSerializer.Deserialize<GltfBufferView>(bufferView) ?? throw new Exception();
    }

    public GltfImage LoadImage(int imageIndex)
    {
        var images = GetRootArray("images");
        var image = images[imageIndex] ?? throw new Exception("images[imageIndex] is null");
        return JsonSerializer.Deserialize<GltfImage>(image) ?? throw new Exception();
    }

    public (string, BufferRange) LoadTextureBytes(int textureIndex)
    {
        var textures = GetRootArray("textures");
        var texture =
            textures[textureIndex] ?? throw new Exception("textures[textureIndex] is null");
        var imageIndex = texture.GetPropertyValue<int>("source");
        var image = LoadImage(imageIndex);

        if (image.bufferView is int bufferViewIndex)
        {
            var bufferView = LoadBufferView(bufferViewIndex);
            var buffer = LoadBuffer(bufferView.buffer);
            var bin = GetOrReadUri(buffer.uri);
            return (
                image.mimeType ?? throw new Exception(""),
                new BufferRange(bufferView.byteOffset, bufferView.byteLength, bin)
            );
        }

        if (image.uri is string uri)
        {
            var bin = GetOrReadUri(image.uri);
            return (image.mimeType ?? GetMimeFromName(uri), new BufferRange(0, bin.Count, bin));
        }

        throw new Exception();
    }

    static string GetMimeFromName(string uri)
    {
        switch (Path.GetExtension(uri).ToLower())
        {
            case ".png":
                return "image/png";

            case ".jpg":
                return "image/jpeg";

            default:
                throw new NotImplementedException($"unknwon mime: {uri}");
        }
    }

    public (BufferRange, GltfAccessor, GltfBufferView) LoadAccessorArray(int accessorIndex)
    {
        var accessors = GetRootArray("accessors");
        var accessor =
            accessors[accessorIndex] ?? throw new Exception($"accessors[{accessorIndex}] is null");
        var gltfAccessor =
            JsonSerializer.Deserialize<GltfAccessor>(accessor) ?? throw new Exception();
        int elementSize = gltfAccessor.GetElementSize();
        var bufferView = LoadBufferView(gltfAccessor.bufferView);
        var buffer = LoadBuffer(bufferView.buffer);
        var bin = GetOrReadUri(buffer.uri);
        return (
            new BufferRange(
                bufferView.byteOffset + gltfAccessor.byteOffset,
                elementSize * gltfAccessor.count,
                bin
            ),
            gltfAccessor,
            bufferView
        );
    }

    public Material[] LoadMaterials()
    {
        var list = new List<Material>();
        var materials = GetRootArray("materials");
        if (materials is not null)
        {
            foreach (var materialNode in materials.AsArray())
            {
                if (materialNode is null)
                {
                    throw new Exception("materialNode is null");
                }
                Drawlist.TextureImage? image = default;
                if (TryGetBaseColorTexture(materialNode, out int textureIndex))
                {
                    var (mime, textureBytes) = LoadTextureBytes(textureIndex);
                    // TODO: ToArray
                    switch (mime)
                    {
                        case "image/jpeg":
                            image = new Drawlist.TextureImage(
                                TextureImageType.Jpg,
                                textureBytes.Slice().ToArray()
                            );
                            break;
                        case "image/png":
                            image = new Drawlist.TextureImage(
                                TextureImageType.Png,
                                textureBytes.Slice().ToArray()
                            );
                            break;
                        default:
                            throw new Exception($"unknwon mime: {mime}");
                    }
                }

                // TODO: PBR
                list.Add(
                    new Material(new("gltf", (Topology.Triangles, Vertex.Layout)), default, image)
                );
            }
        }
        return [.. list];
    }

    private static bool TryGetBaseColorTexture(JsonNode materialNode, out int textureIndex)
    {
        if (materialNode.AsObject().TryGetPropertyValue("pbrMetallicRoughness", out var pbrNode))
        {
            if (pbrNode is null)
            {
                throw new Exception("pbrNode is null");
            }
            if (pbrNode.AsObject().TryGetPropertyValue("baseColorTexture", out var textureNode))
            {
                if (textureNode is null)
                {
                    throw new Exception("textureNode is null");
                }
                textureIndex = textureNode.GetPropertyValue<int>("index");
                return true;
            }
        }

        textureIndex = -1;
        return false;
    }

    public IReadOnlyList<Skin> LoadSkins()
    {
        var list = new List<Skin>();
        if (Root.TryGetPropertyValue("skins", out var skinsNode))
        {
            if (skinsNode is not null)
            {
                foreach (var skinNode in skinsNode.AsArray())
                {
                    var gltfSkin =
                        JsonSerializer.Deserialize<GltfSkin>(skinNode) ?? throw new Exception();
                    var bindMatrices = LoadAccessorArray(
                        gltfSkin.inverseBindMatrices ?? throw new Exception()
                    )
                        .Item1.Slice<Matrix4x4>();
                    var skin = new Skin(gltfSkin.joints, bindMatrices.ToArray());
                    list.Add(skin);
                }
            }
        }
        return list;
    }

    public (Node, IReadOnlyList<Node>) LoadHierarchy(
        IReadOnlyList<Mesh> meshes,
        IReadOnlyList<Skin> skins
    )
    {
        var jsonNodes = GetRootArray("nodes");
        var gltfNodes = new GltfNode[jsonNodes.Count];
        var nodes = new Node[jsonNodes.Count];
        var root = new Node();
        for (int i = 0; i < jsonNodes.Count; ++i)
        {
            var nodeNode = jsonNodes[i];
            var gltfNode = JsonSerializer.Deserialize<GltfNode>(nodeNode) ?? throw new Exception();
            gltfNodes[i] = gltfNode;
            var node = new Node();
            nodes[i] = node;
            root.AddChild(node);
            if (gltfNode.name is string name)
            {
                node.Name = name;
            }
            else
            {
                node.Name = $"__node__:{i}";
            }

            if (gltfNode.mesh is int meshIndex)
            {
                node.Mesh = meshes[meshIndex];
            }
            if (gltfNode.skin is int skinIndex)
            {
                node.Skin = skins[skinIndex];
            }

            if (gltfNode.matrix is float[] m)
            {
                Matrix4x4 matrix;
                // csharpier-ignore
                matrix = new (
                    m[0], m[1], m[2], m[3],
                    m[4], m[5], m[6], m[7],
                    m[8], m[9], m[10], m[11],
                    m[12], m[13], m[14], m[15]
                );
                if (Matrix4x4.Decompose(matrix, out var s, out var r, out var t))
                {
                    node.Scale = s;
                    node.Rotation = r;
                    node.Translation = t;
                }
            }
            else
            {
                if (gltfNode.translation is float[] t)
                {
                    node.Translation = new Vector3(t[0], t[1], t[2]);
                }
                if (gltfNode.rotation is float[] r)
                {
                    node.Rotation = new Quaternion(r[0], r[1], r[2], r[3]);
                }
                if (gltfNode.scale is float[] s)
                {
                    node.Scale = new Vector3(s[0], s[1], s[2]);
                }
            }
        }

        // build hierarchy
        for (int i = 0; i < gltfNodes.Length; ++i)
        {
            var gltfNode = gltfNodes[i];
            if (gltfNode.children is int[] children)
            {
                foreach (var childIndex in children)
                {
                    // move child from root.Children to nodes[i].Children
                    nodes[i].AddChild(nodes[childIndex]);
                }
            }
        }

        return (root, nodes);
    }

    public Animation LoadAnimation(JsonNode? animationNode, IReadOnlyList<Node> nodes)
    {
        if (animationNode is null)
        {
            throw new Exception();
        }
        var gltfAnimation =
            JsonSerializer.Deserialize<GltfAnimation>(animationNode) ?? throw new Exception();
        var animation = new Animation();
        Dictionary<Node, NodeAnimation> nodeMap = [];
        foreach (var gltfChannel in gltfAnimation.channels)
        {
            if (gltfChannel.target.node is int nodeIndex)
            {
                var targetNode = nodes[nodeIndex];
                if (!nodeMap.TryGetValue(targetNode, out NodeAnimation? nodeAnimation))
                {
                    nodeAnimation = new(targetNode);
                    nodeMap.Add(targetNode, nodeAnimation);
                    animation.NodeAnimations.Add(nodeAnimation);
                }

                var gltfAnimationSampler = gltfAnimation.samplers[gltfChannel.sampler];
                var input = LoadAccessorArray(gltfAnimationSampler.input).Item1.Slice<float>();
                switch (gltfChannel.target.path)
                {
                    case "translation":
                    {
                        var output = LoadAccessorArray(gltfAnimationSampler.output)
                            .Item1.Slice<Vector3>();
                        nodeAnimation.T = new Vector3Curve(input.ToArray(), output.ToArray());
                        break;
                    }
                    case "rotation":
                    {
                        var output = LoadAccessorArray(gltfAnimationSampler.output)
                            .Item1.Slice<Quaternion>();
                        nodeAnimation.R = new QuaternionCurve(input.ToArray(), output.ToArray());
                        break;
                    }
                    case "scale":
                    {
                        var output = LoadAccessorArray(gltfAnimationSampler.output)
                            .Item1.Slice<Vector3>();
                        nodeAnimation.S = new Vector3Curve(input.ToArray(), output.ToArray());
                        break;
                    }
                    case "weights":
                        throw new NotImplementedException("weights not impl");

                    default:
                        throw new NotImplementedException();
                }
            }
        }
        animation.CalcDuration();
        return animation;
    }
}
