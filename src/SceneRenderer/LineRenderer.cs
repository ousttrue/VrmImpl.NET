using System.Numerics;
using System.Runtime.InteropServices;
using VrmImpl.Drawlist;
using VrmImpl.Drawlist.Gizmo;
using VrmImpl.SceneGraph;
using VrmImpl.VorticeVulkan;
using Vortice.Vulkan;

namespace VrmImpl.SceneRenderer;

public class LineRenderer : IDisposable
{
    private readonly VkDeviceApi _vkd;

    private static readonly byte[] vsSpv = ShaderResource.FromAssembly("line.vert.spv");
    private static readonly byte[] fsSpv = ShaderResource.FromAssembly("line.frag.spv");

    private readonly VulkanPipeline<Matrix4x4> _pipeline;

    private readonly LineVertex[] _vertices = new LineVertex[ushort.MaxValue];
    private int _pos = 0;

    private readonly ArrayBufferObject[] _stagings;
    private readonly ArrayBufferObject _vertexBuffer;

    private readonly UniformBufferObject<WorldInfo>[] _uniformBuffers;

    public LineRenderer(
        VkInstanceApi vki,
        VkDeviceApi vk,
        VkPhysicalDevice physicalDevice,
        uint graphicsQueueFamilyIndex,
        uint flightCount,
        VkFormat colorFormat,
        VkFormat depthFormat
    )
    {
        _vkd = vk;
        using var vs = new VulkanShaderModule(vk, vsSpv);
        using var fs = new VulkanShaderModule(vk, fsSpv);
        var (binding, attributes) = VkHelper.InputFromLayout(LineVertex.Layout);
        _pipeline = new(
            vk,
            vs,
            fs,
            VkPrimitiveTopology.LineList,
            binding,
            attributes,
            flightCount,
            ShaderResource.DescriptorSetLayoutBindings,
            colorFormat,
            depthFormat,
            ShaderResource.PipelineDepthStencilState
        );

        _vertexBuffer = ArrayBufferObject.Create(
            vki,
            vk,
            VkBufferUsageFlags.VertexBuffer | VkBufferUsageFlags.TransferDst,
            physicalDevice,
            graphicsQueueFamilyIndex,
            _vertices
        );

        _stagings = new ArrayBufferObject[flightCount];
        for (int i = 0; i < _stagings.Length; ++i)
        {
            _stagings[i] = ArrayBufferObject.Create(
                vki,
                vk,
                VkBufferUsageFlags.TransferSrc,
                physicalDevice,
                _vertices
            );
        }

        _uniformBuffers = new UniformBufferObject<WorldInfo>[flightCount];
        for (int i = 0; i < flightCount; i++)
        {
            _uniformBuffers[i] = new(vki, _vkd, physicalDevice);
        }
    }

    public void Dispose()
    {
        foreach (var ubo in _uniformBuffers)
        {
            ubo.Dispose();
        }
        foreach (var staging in _stagings)
        {
            staging.Dispose();
        }
        _vertexBuffer.Dispose();
        _pipeline.Dispose();
    }

    public void Clear()
    {
        _pos = 0;
    }

    public void Push(ReadOnlySpan<LineVertex> vertices)
    {
        vertices.CopyTo(_vertices.AsSpan(_pos, vertices.Length));
        _pos += vertices.Length;
    }

    public void Push(LineVertex v0, LineVertex v1)
    {
        _vertices[_pos++] = v0;
        _vertices[_pos++] = v1;
    }

    public unsafe void Upload(VkCommandBuffer commandBuffer, uint imageIndex)
    {
        var staging = _stagings[imageIndex];
        using (var map = staging.Map())
        {
            _vertices.CopyTo(new Span<LineVertex>(map.ToPointer<LineVertex>(), _vertices.Length));
        }
        VkHelper.CopyBuffer(
            _vkd,
            commandBuffer,
            staging.Buffer,
            _vertexBuffer.Buffer,
            staging.ByteLength
        );
    }

    private LineVertex[] _workBuffer = new LineVertex[65536];

    public void ApplyAnimation(
        IReadOnlyList<Node> nodes,
        uint imageIndex,
        VkCommandBuffer commandBuffer
    )
    {
        foreach (var node in nodes)
        {
            if (node.Skin is Skin skin)
            {
                foreach (var jointNodeIndex in skin.Joints)
                {
                    var jointNode = nodes[jointNodeIndex];
                    var len = MakeJointGizmo(_workBuffer, jointNode.WorldTransform);
                    Push(_workBuffer.AsSpan(0, len));
                }
            }
        }

        Upload(commandBuffer, imageIndex);
    }

    static readonly Vector4 red = new(1, 0, 0, 1);
    static readonly Vector4 green = new(0, 1, 0, 1);
    static readonly Vector4 blue = new(0, 0, 1, 1);

    static int MakeJointGizmo(LineVertex[] buffer, Matrix4x4 m)
    {
        var x = m.X.AsVector3() * 0.05f;
        var y = m.Y.AsVector3() * 0.05f;
        var z = m.Z.AsVector3() * 0.05f;
        var pos = m.W.AsVector3();
        buffer[0] = new LineVertex(Position: pos - x, Color: red);
        buffer[1] = new LineVertex(Position: pos + x, Color: red);
        buffer[2] = new LineVertex(Position: pos - y, Color: green);
        buffer[3] = new LineVertex(Position: pos + y, Color: green);
        buffer[4] = new LineVertex(Position: pos - z, Color: blue);
        buffer[5] = new LineVertex(Position: pos + z, Color: blue);
        return 6;
    }

    public void Render(
        uint frameCount,
        uint imageIndex,
        VkCommandBuffer commandBuffer,
        VkExtent2D extent,
        WorldInfo worldInfo
    )
    {
        var desc = _pipeline.Bind(frameCount, commandBuffer, extent, imageIndex);
        _pipeline.PushConstant(commandBuffer, Matrix4x4.Identity);
        var ubo = _uniformBuffers[imageIndex];
        ubo.Upload(worldInfo);
        UpdateDescriptor(desc, ubo);
        _vertexBuffer.Bind(commandBuffer);
        _vertexBuffer.Draw(commandBuffer, 0, (uint)_pos);
    }

    private unsafe void UpdateDescriptor(
        VkDescriptorSet desc,
        UniformBufferObject<WorldInfo> ubo
    // TextureObject texture,
    )
    {
        // DescriptorImageInfo imageInfo = new()
        // {
        //     ImageLayout = ImageLayout.ShaderReadOnlyOptimal,
        //     ImageView = texture.ImageView,
        //     Sampler = texture.Sampler,
        // };

        VkDescriptorBufferInfo bufferInfo = new()
        {
            buffer = ubo.Buffer,
            offset = 0,
            range = (ulong)Marshal.SizeOf<WorldInfo>(),
        };

        var writes = stackalloc[] {
            new VkWriteDescriptorSet()
            {
                sType = VkStructureType.WriteDescriptorSet,
                dstSet = desc,
                dstBinding = 0,
                dstArrayElement = 0,
                descriptorType = VkDescriptorType.UniformBuffer,
                descriptorCount = 1,
                pBufferInfo = &bufferInfo,
            },
            // new WriteDescriptorSet()
            // {
            //     sType = VkStructureType.WriteDescriptorSet,
            //     DstSet = desc,
            //     DstBinding = 1,
            //     DstArrayElement = 0,
            //     DescriptorType = DescriptorType.CombinedImageSampler,
            //     DescriptorCount = 1,
            //     PImageInfo = &imageInfo,
            // },
        };
        _vkd.vkUpdateDescriptorSets(1, writes, 0, null);
    }
}
