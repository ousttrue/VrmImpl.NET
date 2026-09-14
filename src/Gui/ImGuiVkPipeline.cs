using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using ImGuiNET;
using VrmImpl.VorticeVulkan;
using Vortice.Vulkan;
using VrmImpl.SceneRenderer;

namespace VrmImpl.Gui;

public class ImGuiVkPipeline : IDisposable, SceneTexture.IPipeline
{
    private readonly VkDeviceApi _vkd;

    private const int maxSets = 255;
    private readonly VkDescriptorPool _descriptorPool;
    private readonly VkDescriptorSet[] _descriptorSets;
    private readonly List<VkDescriptorSet> _descriptorSetPool = new();

    public record struct Constant(Vector2 Scale, Vector2 Translate) { }

    public readonly VulkanPipeline<Constant> _pipeline;

    public int _vertBufferIndex;
    public readonly MeshObject[] _vertBuffers;

    public static readonly VkVertexInputBindingDescription binding_desc =
        new VkVertexInputBindingDescription
        {
            stride = (uint)Marshal.SizeOf<ImDrawVert>(),
            inputRate = VkVertexInputRate.Vertex,
        };

    public static readonly VkVertexInputAttributeDescription[] attribute_desc =
    [
        new VkVertexInputAttributeDescription
        {
            location = 0,
            format = VkFormat.R32G32Sfloat,
            offset = (uint)Marshal.OffsetOf<ImDrawVert>(nameof(ImDrawVert.pos)),
        },
        new VkVertexInputAttributeDescription
        {
            location = 1,
            format = VkFormat.R32G32Sfloat,
            offset = (uint)Marshal.OffsetOf<ImDrawVert>(nameof(ImDrawVert.uv)),
        },
        new VkVertexInputAttributeDescription
        {
            location = 2,
            format = VkFormat.R8G8B8A8Unorm,
            offset = (uint)Marshal.OffsetOf<ImDrawVert>(nameof(ImDrawVert.col)),
        },
    ];

    public unsafe ImGuiVkPipeline(
        VkInstanceApi vki,
        VkDeviceApi vkd,
        VkFormat colorFormat,
        VkFormat depthFormat,
        uint swapchainImageCount
    )
    {
        _vkd = vkd;

        _vertBuffers = new MeshObject[swapchainImageCount];
        for (int i = 0; i < _vertBuffers.Length; ++i)
        {
            _vertBuffers[i] = new(
                new ArrayBufferObject(
                    vki,
                    vkd,
                    VkBufferUsageFlags.VertexBuffer,
                    VkMemoryPropertyFlags.HostVisible,
                    (uint)Marshal.SizeOf<ImDrawVert>()
                ),
                new ArrayBufferObject(
                    vki,
                    vkd,
                    VkBufferUsageFlags.IndexBuffer,
                    VkMemoryPropertyFlags.HostVisible,
                    (uint)Marshal.SizeOf<ushort>()
                )
            );
        }
        _vertBufferIndex = 0;

        using var vs = new VulkanShaderModule(
            _vkd,
            MemoryMarshal.Cast<uint, byte>(Shaders.VertexShader)
        );
        using var fs = new VulkanShaderModule(
            _vkd,
            MemoryMarshal.Cast<uint, byte>(Shaders.FragmentShader)
        );
        // DescriptorSetLayout
        VkDescriptorSetLayoutBinding[] bindings =
        [
            new VkDescriptorSetLayoutBinding
            {
                descriptorType = VkDescriptorType.CombinedImageSampler,
                descriptorCount = 1,
                stageFlags = VkShaderStageFlags.Fragment,
            },
        ];
        _pipeline = new(
            _vkd,
            vs,
            fs,
            VkPrimitiveTopology.TriangleList,
            binding_desc,
            attribute_desc,
            swapchainImageCount,
            bindings,
            colorFormat,
            depthFormat,
            new VkPipelineDepthStencilStateCreateInfo
            {
                sType = VkStructureType.PipelineDepthStencilStateCreateInfo,
            }
        );

        //
        // Create the descriptor pool for ImGui
        //
        var poolSize = new VkDescriptorPoolSize
        {
            type = VkDescriptorType.CombinedImageSampler,
            descriptorCount = maxSets,
        };
        var descriptorPoolInfo = new VkDescriptorPoolCreateInfo
        {
            sType = VkStructureType.DescriptorPoolCreateInfo,
            poolSizeCount = 1,
            pPoolSizes = &poolSize,
            maxSets = maxSets,
        };
        _vkd.vkCreateDescriptorPool(in descriptorPoolInfo, default, out _descriptorPool)
            .ThrowIfError();

        VkHelper.AllocateDescriptorSets(
            _vkd,
            _descriptorPool,
            _pipeline.DescriptorSetLayout,
            maxSets,
            out _descriptorSets
        );
        foreach (var desc in _descriptorSets)
        {
            _descriptorSetPool.Add(desc);
        }
    }

    public unsafe void Dispose()
    {
        foreach (var mesh in _vertBuffers)
        {
            mesh.Dispose();
        }

        _pipeline.Dispose();

        _vkd.vkDestroyDescriptorPool(_descriptorPool, default);
    }

    public unsafe VkDescriptorSet BindTexture(TextureObject texture)
    {
        var descImageInfo = new VkDescriptorImageInfo
        {
            sampler = texture.Sampler,
            imageView = texture.ImageView,
            imageLayout = VkImageLayout.ShaderReadOnlyOptimal,
        };

        var desc = _descriptorSetPool[0];
        _descriptorSetPool.RemoveAt(0);

        var writeDescriptors = new VkWriteDescriptorSet
        {
            sType = VkStructureType.WriteDescriptorSet,
            dstSet = desc,
            descriptorCount = 1,
            descriptorType = VkDescriptorType.CombinedImageSampler,
            pImageInfo = &descImageInfo,
        };
        _vkd.vkUpdateDescriptorSets(1, &writeDescriptors, 0, default);

        return desc;
    }

    public void UnbindTexture(VkDescriptorSet texture)
    {
        _descriptorSetPool.Add(texture);
    }

    public unsafe MeshObject? Upload(VkPhysicalDevice physicalDevice, ImDrawDataPtr drawDataPtr)
    {
        _vertBufferIndex = (_vertBufferIndex + 1) % _vertBuffers.Length;
        var vertBuffer = _vertBuffers[_vertBufferIndex];

        var drawData = *drawDataPtr.NativePtr;
        if (drawData.TotalVtxCount <= 0)
        {
            return default;
        }

        var Vertex = vertBuffer.VertexBuffer;
        var Index = vertBuffer.IndexBuffer!;

        // Create or resize the vertex/index buffers
        ulong vertex_size = (ulong)drawData.TotalVtxCount;
        Vertex.Grow(physicalDevice, vertex_size);

        ulong index_size = (ulong)drawData.TotalIdxCount;
        Index.Grow(physicalDevice, index_size);

        // Upload vertex/index data into a single contiguous GPU buffer
        using var vertexMap = Vertex.Map();
        var vtx_dst = vertexMap.ToPointer<ImDrawVert>();
        using var indexMap = Index.Map();
        var idx_dst = indexMap.ToPointer<ushort>();
        for (int n = 0; n < drawData.CmdListsCount; n++)
        {
            ImDrawList* cmd_list = drawDataPtr.CmdLists[n];
            Unsafe.CopyBlock(
                vtx_dst,
                cmd_list->VtxBuffer.Data.ToPointer(),
                (uint)cmd_list->VtxBuffer.Size * (uint)sizeof(ImDrawVert)
            );
            Unsafe.CopyBlock(
                idx_dst,
                cmd_list->IdxBuffer.Data.ToPointer(),
                (uint)cmd_list->IdxBuffer.Size * (uint)sizeof(ushort)
            );
            vtx_dst += cmd_list->VtxBuffer.Size;
            idx_dst += cmd_list->IdxBuffer.Size;
        }

        return vertBuffer;
    }
}
