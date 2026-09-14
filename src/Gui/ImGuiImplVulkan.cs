using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using ImGuiNET;
using Vortice.Vulkan;
using VrmImpl.SceneRenderer;
using VrmImpl.VorticeVulkan;
using static Vortice.Vulkan.Vulkan;

namespace VrmImpl.Gui;

public class ImGuiImplVulkan : IDisposable, SceneTexture.IPipeline
{
    private readonly VkDeviceApi _vd;
    private readonly VkDevice _device;

    private const int maxSets = 255;
    private readonly VkDescriptorPool _descriptorPool;
    private readonly VkDescriptorSet[] _descriptorSets;
    private readonly List<VkDescriptorSet> _descriptorSetPool = new();

    record struct Constant(Vector2 Scale, Vector2 Translate) { }

    private readonly VkPipeline<Constant> _pipeline;

    public int _vertBufferIndex;
    private readonly ImDrawVertBuffer[] _vertBuffers;

    // DescriptorSetLayout
    private readonly VkDescriptorSetLayoutBinding[] bindings =
    [
        new()
        {
            descriptorType = VkDescriptorType.CombinedImageSampler,
            descriptorCount = 1,
            stageFlags = VkShaderStageFlags.Fragment,
        },
    ];

    // VertexInput
    static readonly VkVertexInputBindingDescription binding_desc = new()
    {
        stride = (uint)Unsafe.SizeOf<ImDrawVert>(),
        inputRate = VkVertexInputRate.Vertex,
    };
    static readonly VkVertexInputAttributeDescription[] attribute_desc =
    [
        new()
        {
            location = 0,
            format = VkFormat.R32G32Sfloat,
            offset = (uint)Marshal.OffsetOf<ImDrawVert>(nameof(ImDrawVert.pos)),
        },
        new()
        {
            location = 1,
            format = VkFormat.R32G32Sfloat,
            offset = (uint)Marshal.OffsetOf<ImDrawVert>(nameof(ImDrawVert.uv)),
        },
        new()
        {
            location = 2,
            format = VkFormat.R8G8B8A8Unorm,
            offset = (uint)Marshal.OffsetOf<ImDrawVert>(nameof(ImDrawVert.col)),
        },
    ];

    public unsafe ImGuiImplVulkan(
        VkInstanceApi vi,
        VkDeviceApi vd,
        VkDevice device,
        VkFormat colorFormat,
        uint swapchainImageCount,
        byte[] vsSpv,
        byte[] fsSpv
    )
    {
        _vd = vd;
        _device = device;

        _vertBuffers = new ImDrawVertBuffer[swapchainImageCount];
        for (int i = 0; i < _vertBuffers.Length; ++i)
        {
            _vertBuffers[i] = new(vi, vd);
        }
        _vertBufferIndex = 0;

        using var vs = new ShaderModuleObject(_vd, _device, vsSpv);
        using var fs = new ShaderModuleObject(_vd, _device, fsSpv);
        _pipeline = new(
            _vd,
            _device,
            vs.Module,
            fs.Module,
            VkPrimitiveTopology.TriangleList,
            binding_desc,
            attribute_desc,
            swapchainImageCount,
            bindings,
            colorFormat,
            default
        // depthFormat,
        // new PipelineDepthStencilStateCreateInfo
        // {
        //     SType = VK_STRUCTURE_TYPE_PipelineDepthStencilStateCreateInfo,
        // }
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
            sType = VK_STRUCTURE_TYPE_DESCRIPTOR_POOL_CREATE_INFO,
            poolSizeCount = 1,
            pPoolSizes = &poolSize,
            maxSets = maxSets,
        };
        if (
            _vd.vkCreateDescriptorPool(in descriptorPoolInfo, default, out _descriptorPool)
            != VK_SUCCESS
        )
        {
            throw new Exception($"Unable to create descriptor pool");
        }

        AllocateDescriptorSets(
            _vd,
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

    private static readonly Assembly assm = Assembly.GetExecutingAssembly();

    public static byte[] FromAssembly(string name)
    {
        using var stream =
            assm.GetManifestResourceStream(name)
            ?? throw new Exception($"GetManifestResourceStream: {name}");
        // var reader = new StreamReader(stream);
        // return reader.ReadToEnd();
        using (MemoryStream ms = new MemoryStream())
        {
            stream.CopyTo(ms);
            return ms.ToArray();
        }
    }

    public static unsafe void AllocateDescriptorSets(
        VkDeviceApi vd,
        VkDescriptorPool pool,
        VkDescriptorSetLayout layout,
        uint maxSets,
        out VkDescriptorSet[] descriptorSets
    )
    {
        descriptorSets = new VkDescriptorSet[maxSets];

        var layouts = stackalloc VkDescriptorSetLayout[(int)maxSets];
        new Span<VkDescriptorSetLayout>(layouts, (int)maxSets).Fill(layout);
        var allocateInfo = new VkDescriptorSetAllocateInfo
        {
            sType = VK_STRUCTURE_TYPE_DESCRIPTOR_SET_ALLOCATE_INFO,
            descriptorPool = pool,
            descriptorSetCount = maxSets,
            pSetLayouts = layouts,
        };
        fixed (VkDescriptorSet* descriptorSetsPtr = descriptorSets)
        {
            if (vd.vkAllocateDescriptorSets(in allocateInfo, descriptorSetsPtr) != VK_SUCCESS)
            {
                throw new Exception("failed to allocate descriptor sets!");
            }
        }
    }

    public unsafe void Dispose()
    {
        foreach (var mesh in _vertBuffers)
        {
            mesh.Dispose();
        }
        _pipeline.Dispose();
        _vd.vkDestroyDescriptorPool(_descriptorPool, default);
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
            sType = VK_STRUCTURE_TYPE_WRITE_DESCRIPTOR_SET,
            dstSet = desc,
            descriptorCount = 1,
            descriptorType = VkDescriptorType.CombinedImageSampler,
            pImageInfo = &descImageInfo,
        };
        _vd.vkUpdateDescriptorSets(1, &writeDescriptors, 0, default);

        return desc;
    }

    public void UnbindTexture(VkDescriptorSet texture)
    {
        _descriptorSetPool.Add(texture);
    }

    public void SetFontTexture(VkDescriptorSet fontTexture)
    {
        //     SetFontID(fontTexture.Handle);
        // }
        // private void SetFontID(ulong handle)
        // {
        ImGuiNET.ImGui.GetIO().Fonts.SetTexID((IntPtr)fontTexture.Handle);
        // BeginFrame();
    }

    public unsafe void RenderImDrawData(
        VkPhysicalDevice physicalDevice,
        in ImDrawDataPtr drawDataPtr,
        in VkCommandBuffer commandBuffer,
        uint imageIndex,
        in VkExtent2D swapChainExtent
    )
    {
        int framebufferWidth = (int)(drawDataPtr.DisplaySize.X * drawDataPtr.FramebufferScale.X);
        int framebufferHeight = (int)(drawDataPtr.DisplaySize.Y * drawDataPtr.FramebufferScale.Y);
        if (framebufferWidth <= 0 || framebufferHeight <= 0)
        {
            return;
        }

        // Avoid rendering when minimized, scale coordinates for retina displays (screen coordinates != framebuffer coordinates)
        var drawData = *drawDataPtr.NativePtr;
        int fb_width = (int)(drawData.DisplaySize.X * drawData.FramebufferScale.X);
        int fb_height = (int)(drawData.DisplaySize.Y * drawData.FramebufferScale.Y);
        if (fb_width <= 0 || fb_height <= 0)
        {
            return;
        }

        // Allocate array to store enough vertex/index buffers

        _vertBufferIndex = (_vertBufferIndex + 1) % _vertBuffers.Length;
        var vertBuffer = _vertBuffers[_vertBufferIndex];
        // update VertexBuffer
        vertBuffer.UploadDrawData(physicalDevice, _device, drawDataPtr);

        // Bind Vertex And Index Buffer:
        if (drawData.TotalVtxCount > 0)
        {
            vertBuffer.Vertex.Bind(commandBuffer);
            vertBuffer.Index.Bind(commandBuffer);
        }

        // Setup viewport:
        var viewport = new VkViewport
        {
            x = 0,
            y = 0,
            width = (float)fb_width,
            height = (float)fb_height,
            minDepth = 0.0f,
            maxDepth = 1.0f,
        };
        _vd.vkCmdSetViewport(commandBuffer, 0, 1, &viewport);

        // Setup scale and translation:
        // Our visible imgui space lies from draw_data.DisplayPps (top left) to draw_data.DisplayPos+data_data.DisplaySize (bottom right). DisplayPos is (0,0) for single viewport apps.
        // Span<float> scale = stackalloc float[2];
        var scale = new Vector2(2.0f / drawData.DisplaySize.X, 2.0f / drawData.DisplaySize.Y);
        var translate = new Vector2(
            -1.0f - drawData.DisplayPos.X * scale[0],
            -1.0f - drawData.DisplayPos.Y * scale[1]
        );
        _pipeline.PushConstant(commandBuffer, new Constant(scale, translate));

        // Will project scissor/clipping rectangles into framebuffer space
        Vector2 clipOff = drawData.DisplayPos; // (0,0) unless using multi-viewports
        Vector2 clipScale = drawData.FramebufferScale; // (1,1) unless using retina display which are often (2,2)

        // Render command lists
        // (Because we merged all buffers into a single one, we maintain our own offset into them)
        nint last_image_view = -1;
        int vertexOffset = 0;
        int indexOffset = 0;
        for (int n = 0; n < drawData.CmdListsCount; n++)
        {
            var cmd_list = drawDataPtr.CmdLists[n];
            for (int cmd_i = 0; cmd_i < cmd_list.CmdBuffer.Size; cmd_i++)
            {
                var pcmd = cmd_list.CmdBuffer[cmd_i];

                // Project scissor/clipping rectangles into framebuffer space
                Vector4 clipRect;
                clipRect.X = (pcmd.ClipRect.X - clipOff.X) * clipScale.X;
                clipRect.Y = (pcmd.ClipRect.Y - clipOff.Y) * clipScale.Y;
                clipRect.Z = (pcmd.ClipRect.Z - clipOff.X) * clipScale.X;
                clipRect.W = (pcmd.ClipRect.W - clipOff.Y) * clipScale.Y;

                if (
                    clipRect.X < fb_width
                    && clipRect.Y < fb_height
                    && clipRect.Z >= 0.0f
                    && clipRect.W >= 0.0f
                )
                {
                    // Negative offsets are illegal for vkCmdSetScissor
                    if (clipRect.X < 0.0f)
                        clipRect.X = 0.0f;
                    if (clipRect.Y < 0.0f)
                        clipRect.Y = 0.0f;

                    // Apply scissor/clipping rectangle
                    VkRect2D scissor = new();
                    scissor.offset.x = (int)clipRect.X;
                    scissor.offset.y = (int)clipRect.Y;
                    scissor.extent.width = (uint)(clipRect.Z - clipRect.X);
                    scissor.extent.height = (uint)(clipRect.W - clipRect.Y);
                    _vd.vkCmdSetScissor(commandBuffer, 0, 1, &scissor);

                    // TODO
                    // https://github.com/ocornut/imgui/blob/master/backends/imgui_impl_vulkan.cpp#L553
                    // Bind DescriptorSets for image view (font or user texture) and samplers
                    var image_view = pcmd.GetTexID();
                    if (image_view != last_image_view)
                    {
                        var descriptorSet = new VkDescriptorSet((ulong)image_view);
                        _pipeline.Bind(commandBuffer, swapChainExtent, descriptorSet);
                    }
                    last_image_view = image_view;

                    // Draw
                    _vd.vkCmdDrawIndexed(
                        commandBuffer,
                        pcmd.ElemCount,
                        1,
                        pcmd.IdxOffset + (uint)indexOffset,
                        (int)pcmd.VtxOffset + vertexOffset,
                        0
                    );
                }
            }
            indexOffset += cmd_list.IdxBuffer.Size;
            vertexOffset += cmd_list.VtxBuffer.Size;
        }
    }
}
