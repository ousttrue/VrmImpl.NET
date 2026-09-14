using System.Numerics;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using VrmImpl.Drawlist;

namespace VrmImpl.SceneGraph;

/// <summary>
/// byte, ushort, uint の index を byte[] に cast して保持してみたが
/// むしろ不便かも。
///
/// uint[] 決め打ちで良いのではないか。
/// </summary>
public static class GltfMeshLoader
{
    public static List<Mesh> LoadMeshes(this GltfLoader gltf, Material[] materials)
    {
        var list = new List<Mesh>();
        var meshesNode = gltf.GetRootArray("meshes");
        foreach (var meshNode in meshesNode)
        {
            var mesh = LoadMesh(gltf, meshNode, materials);
            if (mesh is not null)
            {
                list.Add(mesh);
            }
        }
        return list;
    }

    public static Mesh? LoadMesh(GltfLoader gltf, JsonNode? meshNode, Material[] materials)
    {
        if (meshNode is null)
        {
            return null;
        }

        if (!meshNode.AsObject().TryGetPropertyValue("primitives", out var primsNode))
        {
            return null;
        }
        var prims = (primsNode ?? throw new Exception("primsNode is null")).AsArray();

        var accessors = gltf.GetRootArray("accessors");
        var (vertexCount, _indexType, indexCount, primitives, shared) = CountVertexAndIndex(
            gltf,
            accessors,
            prims,
            materials
        );

        VertexInfo vertices;
        SkinVertex[] skinVertices = [];
        IndexInfo? indices = default;
        if (shared)
        {
            // vrm-0.x legacy
            if (_indexType is GltfAccessor.ComponentType indexType)
            {
                switch (indexType)
                {
                    case GltfAccessor.ComponentType.UBYTE:
                        (vertices, skinVertices, indices) = LoadMeshSharedVertexBuffer<byte>(
                            gltf,
                            prims,
                            vertexCount,
                            indexCount,
                            x => (uint)x
                        );
                        break;
                    case GltfAccessor.ComponentType.USHORT:
                        (vertices, skinVertices, indices) = LoadMeshSharedVertexBuffer<ushort>(
                            gltf,
                            prims,
                            vertexCount,
                            indexCount,
                            x => (uint)x
                        );
                        break;
                    case GltfAccessor.ComponentType.UINT:
                        (vertices, skinVertices, indices) = LoadMeshSharedVertexBuffer<uint>(
                            gltf,
                            prims,
                            vertexCount,
                            indexCount,
                            x => x
                        );
                        break;
                    default:
                        throw new Exception();
                }
            }
            else
            {
                (vertices, skinVertices) = LoadMeshSharedVertexBufferWithoutIndex(
                    gltf,
                    prims,
                    vertexCount
                );
            }
        }
        else
        {
            // glTF general
            if (_indexType is GltfAccessor.ComponentType indexType)
            {
                switch (indexType)
                {
                    case GltfAccessor.ComponentType.UBYTE:
                        (vertices, skinVertices, indices) = LoadMeshJoinPrimitiveVertexBuffer<byte>(
                            gltf,
                            prims,
                            vertexCount,
                            indexCount,
                            (
                                ReadOnlySpan<byte> indexSpan,
                                Span<uint> indices,
                                int indicesIndex,
                                int vertexOffset
                            ) =>
                            {
                                foreach (var index in indexSpan)
                                {
                                    indices[indicesIndex++] = (byte)(index + vertexOffset);
                                }
                                return indicesIndex;
                            }
                        );
                        break;
                    case GltfAccessor.ComponentType.USHORT:
                        (vertices, skinVertices, indices) =
                            LoadMeshJoinPrimitiveVertexBuffer<ushort>(
                                gltf,
                                prims,
                                vertexCount,
                                indexCount,
                                (
                                    ReadOnlySpan<ushort> indexSpan,
                                    Span<uint> indices,
                                    int indicesIndex,
                                    int vertexOffset
                                ) =>
                                {
                                    foreach (var index in indexSpan)
                                    {
                                        indices[indicesIndex++] = (ushort)(index + vertexOffset);
                                    }
                                    return indicesIndex;
                                }
                            );
                        break;
                    case GltfAccessor.ComponentType.UINT:
                        (vertices, skinVertices, indices) = LoadMeshJoinPrimitiveVertexBuffer<uint>(
                            gltf,
                            prims,
                            vertexCount,
                            indexCount,
                            (
                                ReadOnlySpan<uint> indexSpan,
                                Span<uint> indices,
                                int indicesIndex,
                                int vertexOffset
                            ) =>
                            {
                                foreach (var index in indexSpan)
                                {
                                    indices[indicesIndex++] = (uint)(index + vertexOffset);
                                }
                                return indicesIndex;
                            }
                        );
                        break;
                    default:
                        throw new Exception();
                }
            }
            else
            {
                (vertices, skinVertices) = LoadMeshJoinPrimitiveVertexBufferWithout(
                    gltf,
                    prims,
                    vertexCount
                );
            }
        }

        var mesh = new Mesh(vertices, indices, primitives, skinVertices);
        return mesh;
    }

    record struct MeshInfo(
        int VertexCount,
        GltfAccessor.ComponentType? IndexType,
        int IndexCount,
        List<Primitive> Primitives,
        bool SharedAttributes
    ) { }

    static MeshInfo CountVertexAndIndex(
        GltfLoader gltf,
        JsonArray accessors,
        JsonArray prims,
        Material[] materials
    )
    {
        int vertexCount = 0;
        GltfAccessor.ComponentType? _indexType = default;
        int indexCount = 0;
        var primitives = new List<Primitive>();
        var shared = false;
        GltfMeshPrimitiveAttributes? _lastAttributes = default;
        foreach (var prim in prims)
        {
            if (prim is null)
            {
                throw new Exception("prim is null");
            }
            prim.AsObject().TryGetPropertyValue("attributes", out var attributes);
            if (attributes is null)
            {
                throw new Exception("no attributes");
            }
            var gltfAttributes =
                JsonSerializer.Deserialize<GltfMeshPrimitiveAttributes>(attributes)
                ?? throw new Exception();
            if (_lastAttributes is GltfMeshPrimitiveAttributes lastAttributes)
            {
                shared = lastAttributes.Equals(gltfAttributes);
            }

            var materialIndex = prim.GetPropertyValue<int>("material");

            // position
            var positionGltfAccessor = gltf.LoadAccessorArray(gltfAttributes.POSITION).Item2;
            if (!shared)
            {
                vertexCount += positionGltfAccessor.count;
            }

            _lastAttributes = gltfAttributes;

            // indices
            prim.AsObject().TryGetPropertyValue("indices", out var indicesNode);
            if (indicesNode is not null)
            {
                var indicesAccessorIndex = indicesNode.GetValue<int>();
                var indicesAccessor =
                    accessors[indicesAccessorIndex] ?? throw new Exception("accessor is null");
                var indicesGltfAccessor =
                    JsonSerializer.Deserialize<GltfAccessor>(indicesAccessor)
                    ?? throw new Exception();

                primitives.Add(
                    new Primitive(indexCount, indicesGltfAccessor.count, materials[materialIndex])
                );

                if (_indexType is GltfAccessor.ComponentType indexType)
                {
                    if (indexType != indicesGltfAccessor.componentType)
                    {
                        throw new Exception("Invalid accessor. Different index type in prims");
                    }
                }
                else
                {
                    _indexType = indicesGltfAccessor.componentType;
                }

                indexCount += indicesGltfAccessor.count;
            }
        }
        return new(vertexCount, _indexType, indexCount, primitives, shared);
    }

    public ref struct VertexSpans
    {
        public ReadOnlySpan<Vector3> POSITION;
        public ReadOnlySpan<Vector2> TEXCOORD_0;
        public ReadOnlySpan<Vector4> JOINTS_0;
        public ReadOnlySpan<Vector4> WEIGHTS_0;

        public int Push(Span<Vertex> vertices, SkinVertex[] skinVertices, int vertexIndex)
        {
            for (int i = 0; i < POSITION.Length; ++i)
            {
                vertices[vertexIndex].Position = POSITION[i];
                if (TEXCOORD_0.Length == POSITION.Length)
                {
                    vertices[vertexIndex].TexCoords = TEXCOORD_0[i];
                }
                if (JOINTS_0.Length == POSITION.Length && WEIGHTS_0.Length == POSITION.Length)
                {
                    skinVertices[vertexIndex] = new SkinVertex(JOINTS_0[i], WEIGHTS_0[i]);
                }
                ++vertexIndex;
            }
            return vertexIndex;
        }
    }

    static VertexSpans LoadAttributes(GltfLoader gltf, GltfMeshPrimitiveAttributes attributes)
    {
        VertexSpans spans = default;
        spans.POSITION = gltf.LoadAccessorArray(attributes.POSITION).Item1.Slice<Vector3>();
        if (attributes.TEXCOORD_0 is int tex0AccessorIndex)
        {
            spans.TEXCOORD_0 = gltf.LoadAccessorArray(tex0AccessorIndex).Item1.Slice<Vector2>();
        }
        if (attributes.JOINTS_0 is int joints0AccessorIndex)
        {
            var (range, accessor, _) = gltf.LoadAccessorArray(joints0AccessorIndex);
            switch (accessor.componentType)
            {
                case GltfAccessor.ComponentType.USHORT:
                    {
                        // UShort4 to Vector4
                        var span = range.Slice<UShort4>();
                        var joints0 = new Vector4[span.Length];
                        for (int i = 0; i < span.Length; ++i)
                        {
                            var x = span[i];
                            joints0[i] = new Vector4(x.X, x.Y, x.Z, x.W);
                        }
                        spans.JOINTS_0 = joints0;
                    }
                    break;

                case GltfAccessor.ComponentType.UBYTE:
                    {
                        // UShort4 to Vector4
                        var span = range.Slice<Byte4>();
                        var joints0 = new Vector4[span.Length];
                        for (int i = 0; i < span.Length; ++i)
                        {
                            var x = span[i];
                            joints0[i] = new Vector4(x.X, x.Y, x.Z, x.W);
                        }
                        spans.JOINTS_0 = joints0;
                    }
                    break;

                default:
                    throw new NotImplementedException();
            }
        }
        if (attributes.WEIGHTS_0 is int weights0AccessorIndex)
        {
            spans.WEIGHTS_0 = gltf.LoadAccessorArray(weights0AccessorIndex).Item1.Slice<Vector4>();
        }
        return spans;
    }

    /// <summary>
    /// Each primitive use same vertexbuffer but different indexbuffer.
    ///
    ///          +--------+    +-join-+
    /// prim0 => |vertex0 | <= |index0|
    /// prim1 => |(already| <= |index1|
    /// prim2 => |joined) | <= |index2|
    ///          +--------+    +------+
    ///
    /// vertex0 == vertex1 == vertex2 (same buffer reference)
    /// </summary>
    static (VertexInfo, SkinVertex[], IndexInfo) LoadMeshSharedVertexBuffer<IndexType>(
        GltfLoader gltf,
        JsonArray prims,
        int vertexCount,
        int indexCount,
        Func<IndexType, uint> toUint
    )
        where IndexType : unmanaged
    {
        var verticesData = new byte[vertexCount * Marshal.SizeOf<Vertex>()];
        var vertices = MemoryMarshal.Cast<byte, Vertex>(verticesData.AsSpan());
        var skinVertices = new SkinVertex[vertexCount];
        {
            // VertexBuffer is already merged. use prims[0] once.
            var prim = prims[0] ?? throw new Exception("prim is null");
            if (!prim.AsObject().TryGetPropertyValue("attributes", out var attributesNode))
            {
                throw new Exception("no attributes");
            }
            if (attributesNode is null)
            {
                throw new Exception("attributes is null");
            }

            var attributes =
                JsonSerializer.Deserialize<GltfMeshPrimitiveAttributes>(attributesNode)
                ?? throw new Exception("Deserialize MeshAttributes");

            var spans = LoadAttributes(gltf, attributes);
            spans.Push(vertices, skinVertices, 0);
        }

        var indicesData = new byte[indexCount * Marshal.SizeOf<uint>()];
        var indices = MemoryMarshal.Cast<byte, uint>(indicesData.AsSpan());
        if (indexCount > 0)
        {
            var indicesIndex = 0;
            foreach (var prim in prims)
            {
                if (prim is null)
                {
                    throw new Exception("no attributes");
                }

                // join indices
                var indicesAccessorIndex = prim.GetPropertyValue<int>("indices");
                var indicesSpan = gltf.LoadAccessorArray(indicesAccessorIndex)
                    .Item1.Slice<IndexType>();
                foreach (var index in indicesSpan)
                {
                    indices[indicesIndex++] = toUint(index);
                }
            }
        }

        return (new(Vertex.Layout, verticesData), skinVertices, new(4, indicesData));
    }

    static (VertexInfo, SkinVertex[]) LoadMeshSharedVertexBufferWithoutIndex(
        GltfLoader gltf,
        JsonArray prims,
        int vertexCount
    )
    {
        var verticesData = new byte[vertexCount * Marshal.SizeOf<Vertex>()];
        var vertices = MemoryMarshal.Cast<byte, Vertex>(verticesData.AsSpan());
        var skinVertices = new SkinVertex[vertexCount];
        {
            // VertexBuffer is already merged. use prims[0] once.
            var prim = prims[0] ?? throw new Exception("prim is null");
            if (!prim.AsObject().TryGetPropertyValue("attributes", out var attributesNode))
            {
                throw new Exception("no attributes");
            }
            if (attributesNode is null)
            {
                throw new Exception("attributes is null");
            }

            var attributes =
                JsonSerializer.Deserialize<GltfMeshPrimitiveAttributes>(attributesNode)
                ?? throw new Exception("Deserialize MeshAttributes");

            var spans = LoadAttributes(gltf, attributes);
            spans.Push(vertices, skinVertices, 0);
        }

        return (new(Vertex.Layout, verticesData), skinVertices);
    }

    /// <summary>
    /// Join vertexbuffer and indexbuffer for prims and modify index.
    ///
    ///          +--join-+    +--join and add offset ----------+
    /// prim0 => |vertex0| <= |index0                          |
    /// prim1 => |vertex1| <= |index1(+vertex0.len)            |
    /// prim2 => |vertex2| <= |index2(+vertex0.len+vertex1.len)|
    ///          +-------+    +--------------------------------+
    /// </summary>
    static (VertexInfo, SkinVertex[], IndexInfo) LoadMeshJoinPrimitiveVertexBuffer<IndexType>(
        GltfLoader gltf,
        JsonArray prims,
        int vertexCount,
        int indexCount,
        Func<ReadOnlySpan<IndexType>, Span<uint>, int, int, int> copyOffsetIndex
    )
        where IndexType : unmanaged
    {
        var verticesData = new byte[vertexCount * Marshal.SizeOf<Vertex>()];
        var vertices = MemoryMarshal.Cast<byte, Vertex>(verticesData.AsSpan());
        var skinVertices = new SkinVertex[vertexCount];
        var vertexIndex = 0;
        var indicesData = new byte[indexCount * 4];
        var indices = MemoryMarshal.Cast<byte, uint>(indicesData.AsSpan());
        var indicesIndex = 0;
        foreach (var prim in prims)
        {
            if (
                prim is null
                || !prim.AsObject().TryGetPropertyValue("attributes", out var attributesNode)
            )
            {
                throw new Exception("no attributes");
            }
            if (attributesNode is null)
            {
                throw new Exception("attributes is null");
            }

            var attributes =
                JsonSerializer.Deserialize<GltfMeshPrimitiveAttributes>(attributesNode)
                ?? throw new Exception("Deserialize MeshAttributes");

            // join vertices
            var spans = LoadAttributes(gltf, attributes);
            var vertexOffset = vertexIndex;
            vertexIndex = spans.Push(vertices, skinVertices, vertexIndex);

            if (indexCount > 0)
            {
                // join indices and vertexOffset
                var indicesAccessorIndex = prim.GetPropertyValue<int>("indices");
                var indexSpan = gltf.LoadAccessorArray(indicesAccessorIndex)
                    .Item1.Slice<IndexType>();
                indicesIndex = copyOffsetIndex(indexSpan, indices, indicesIndex, vertexOffset);
            }
        }
        return (new(Vertex.Layout, verticesData), skinVertices, new(4, indicesData));
    }

    static (VertexInfo, SkinVertex[]) LoadMeshJoinPrimitiveVertexBufferWithout(
        GltfLoader gltf,
        JsonArray prims,
        int vertexCount
    )
    {
        var data = new byte[vertexCount * Marshal.SizeOf<Vertex>()];
        var vertices = MemoryMarshal.Cast<byte, Vertex>(data.AsSpan());
        var skinVertices = new SkinVertex[vertexCount];
        var vertexIndex = 0;
        foreach (var prim in prims)
        {
            if (
                prim is null
                || !prim.AsObject().TryGetPropertyValue("attributes", out var attributesNode)
            )
            {
                throw new Exception("no attributes");
            }
            if (attributesNode is null)
            {
                throw new Exception("attributes is null");
            }

            var attributes =
                JsonSerializer.Deserialize<GltfMeshPrimitiveAttributes>(attributesNode)
                ?? throw new Exception("Deserialize MeshAttributes");

            // join vertices
            var spans = LoadAttributes(gltf, attributes);
            var vertexOffset = vertexIndex;
            vertexIndex = spans.Push(vertices, skinVertices, vertexIndex);
        }
        return (new(Vertex.Layout, data), skinVertices);
    }
}
