using System.Numerics;
using System.Reflection;
using System.Runtime.InteropServices;
using VrmImpl.Drawlist;
using Vortice.Vulkan;

namespace VrmImpl.VorticeVulkan;

public static class ShaderResource
{
    private static readonly Assembly assm = Assembly.GetExecutingAssembly();

    public static byte[] FromAssembly(string name)
    {
        using var stream =
            assm.GetManifestResourceStream(name)
            ?? throw new Exception($"GetManifestResourceStream: {assm} => {name}");
        // var reader = new StreamReader(stream);
        // return reader.ReadToEnd();
        using (MemoryStream ms = new MemoryStream())
        {
            stream.CopyTo(ms);
            return ms.ToArray();
        }
    }

    public static readonly byte[] ImageBytes = FromAssembly("texture.png");
    public static readonly byte[] VertexSpv = FromAssembly("27_shader_depth.vert.spv");
    public static readonly byte[] FragmentSpv = FromAssembly("27_shader_depth.frag.spv");

    public static readonly VkDescriptorSetLayoutBinding[] DescriptorSetLayoutBindings =
    [
        new VkDescriptorSetLayoutBinding
        {
            binding = 0,
            descriptorCount = 1,
            descriptorType = VkDescriptorType.UniformBuffer,
            pImmutableSamplers = null,
            stageFlags = VkShaderStageFlags.Vertex,
        },
        new VkDescriptorSetLayoutBinding
        {
            binding = 1,
            descriptorCount = 1,
            descriptorType = VkDescriptorType.CombinedImageSampler,
            pImmutableSamplers = null,
            stageFlags = VkShaderStageFlags.Fragment,
        },
    ];

    public static readonly VkPipelineDepthStencilStateCreateInfo PipelineDepthStencilState =
        new VkPipelineDepthStencilStateCreateInfo
        {
            sType = VkStructureType.PipelineDepthStencilStateCreateInfo,
            depthTestEnable = true,
            depthWriteEnable = true,
            depthCompareOp = VkCompareOp.Less,
            depthBoundsTestEnable = false,
            stencilTestEnable = false,
        };

    public struct Vertex
    {
        public Vector3 pos;
        public Vector3 color;
        public Vector2 textCoord;

        public static readonly FloatVertexLayout[] Layout =
        [
            new(0, 3, 32, 0),
            new(1, 3, 32, 12),
            new(2, 2, 32, 24),
        ];
    }

    public static readonly Vertex[] Vertices =
    [
        new Vertex
        {
            pos = new(-0.5f, -0.5f, 0.0f),
            color = new(1.0f, 0.0f, 0.0f),
            textCoord = new(1.0f, 0.0f),
        },
        new Vertex
        {
            pos = new(0.5f, -0.5f, 0.0f),
            color = new(0.0f, 1.0f, 0.0f),
            textCoord = new(0.0f, 0.0f),
        },
        new Vertex
        {
            pos = new(0.5f, 0.5f, 0.0f),
            color = new(0.0f, 0.0f, 1.0f),
            textCoord = new(0.0f, 1.0f),
        },
        new Vertex
        {
            pos = new(-0.5f, 0.5f, 0.0f),
            color = new(1.0f, 1.0f, 1.0f),
            textCoord = new(1.0f, 1.0f),
        },
        new Vertex
        {
            pos = new(-0.5f, -0.5f, -0.5f),
            color = new(1.0f, 0.0f, 0.0f),
            textCoord = new(1.0f, 0.0f),
        },
        new Vertex
        {
            pos = new(0.5f, -0.5f, -0.5f),
            color = new(0.0f, 1.0f, 0.0f),
            textCoord = new(0.0f, 0.0f),
        },
        new Vertex
        {
            pos = new(0.5f, 0.5f, -0.5f),
            color = new(0.0f, 0.0f, 1.0f),
            textCoord = new(0.0f, 1.0f),
        },
        new Vertex
        {
            pos = new(-0.5f, 0.5f, -0.5f),
            color = new(1.0f, 1.0f, 1.0f),
            textCoord = new(1.0f, 1.0f),
        },
    ];

    public static readonly VertexInfo VertexInfo = new(
        Vertex.Layout,
        MemoryMarshal.Cast<Vertex, byte>(Vertices.AsSpan()).ToArray()
    );

    public static readonly ushort[] Indices = [0, 1, 2, 2, 3, 0, 4, 5, 6, 6, 7, 4];

    public static readonly IndexInfo IndexInfo = new(
        4,
        MemoryMarshal.Cast<ushort, byte>(Indices.AsSpan()).ToArray()
    );

    public static VkVertexInputBindingDescription GetBindingDescription()
    {
        VkVertexInputBindingDescription bindingDescription = new()
        {
            binding = 0,
            stride = (uint)Marshal.SizeOf<Vertex>(),
            inputRate = VkVertexInputRate.Vertex,
        };

        return bindingDescription;
    }

    public static VkVertexInputAttributeDescription[] GetAttributeDescriptions()
    {
        var attributeDescriptions = new[]
        {
            new VkVertexInputAttributeDescription()
            {
                binding = 0,
                location = 0,
                format = VkFormat.R32G32B32Sfloat,
                offset = (uint)Marshal.OffsetOf<Vertex>(nameof(Vertex.pos)),
            },
            new VkVertexInputAttributeDescription()
            {
                binding = 0,
                location = 1,
                format = VkFormat.R32G32B32Sfloat,
                offset = (uint)Marshal.OffsetOf<Vertex>(nameof(Vertex.color)),
            },
            new VkVertexInputAttributeDescription()
            {
                binding = 0,
                location = 2,
                format = VkFormat.R32G32Sfloat,
                offset = (uint)Marshal.OffsetOf<Vertex>(nameof(Vertex.textCoord)),
            },
        };

        return attributeDescriptions;
    }
}
