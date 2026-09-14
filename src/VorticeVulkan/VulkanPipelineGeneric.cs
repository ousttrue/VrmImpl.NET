using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Vortice.Vulkan;

namespace VrmImpl.VorticeVulkan;

public class VulkanPipeline<CONSTANT> : IDisposable
    where CONSTANT : unmanaged
{
    VkDeviceApi _vkd;

    public readonly VkDescriptorSetLayout DescriptorSetLayout;
    public readonly DescriptorPoolObject[] DescriptorPools;

    public readonly VkPipelineLayout PipelieLayout;
    private readonly VkPipeline _graphicsPipeline;

    public record struct RenderPassArgs(
        VkExtent2D extent,
        VkImageView[] imageViews,
        VkImageView depthImageView
    ) { }

    public unsafe VulkanPipeline(
        VkDeviceApi vkd,
        VulkanShaderModule vs,
        VulkanShaderModule fs,
        VkPrimitiveTopology topology,
        VkVertexInputBindingDescription vertexInputBindingDescription,
        ReadOnlySpan<VkVertexInputAttributeDescription> vertexInputAttributeDescriptions,
        uint maxFlightCount,
        ReadOnlySpan<VkDescriptorSetLayoutBinding> descriptorSetLayoutBindings,
        VkFormat colorFormat,
        VkFormat depthFormat,
        VkPipelineDepthStencilStateCreateInfo depthStencil
    )
    {
        _vkd = vkd;

        //
        // descriptorSetLayout
        //
        fixed (VkDescriptorSetLayoutBinding* bindingsPtr = descriptorSetLayoutBindings)
        {
            VkDescriptorSetLayoutCreateInfo layoutInfo = new()
            {
                sType = VkStructureType.DescriptorSetLayoutCreateInfo,
                bindingCount = (uint)descriptorSetLayoutBindings.Length,
                pBindings = bindingsPtr,
            };
            vkd.vkCreateDescriptorSetLayout(in layoutInfo, null, out DescriptorSetLayout)
                .ThrowIfError();
        }

        //
        // descriptorPool
        //
        DescriptorPools = new DescriptorPoolObject[maxFlightCount];
        for (int i = 0; i < DescriptorPools.Length; ++i)
        {
            DescriptorPools[i] = new DescriptorPoolObject(
                _vkd,
                DescriptorSetLayout,
                descriptorSetLayoutBindings,
                255
            );
        }

        var constantRange = new VkPushConstantRange
        {
            offset = 0,
            size = (uint)Marshal.SizeOf<CONSTANT>(),
            stageFlags = VkShaderStageFlags.Vertex,
        };

        //
        // pipeline
        //
        var descriptorSetLayout = DescriptorSetLayout;
        VkPipelineLayoutCreateInfo pipelineLayoutInfo = new()
        {
            sType = VkStructureType.PipelineLayoutCreateInfo,
            setLayoutCount = 1,
            pSetLayouts = &descriptorSetLayout,
            pushConstantRangeCount = 1,
            pPushConstantRanges = &constantRange,
        };
        vkd.vkCreatePipelineLayout(in pipelineLayoutInfo, null, out PipelieLayout).ThrowIfError();

        //
        VkPipelineShaderStageCreateInfo vertShaderStageInfo = new()
        {
            sType = VkStructureType.PipelineShaderStageCreateInfo,
            stage = VkShaderStageFlags.Vertex,
            module = vs.Module,
            pName = new VkUtf8ReadOnlyString("main"u8),
        };
        VkPipelineShaderStageCreateInfo fragShaderStageInfo = new()
        {
            sType = VkStructureType.PipelineShaderStageCreateInfo,
            stage = VkShaderStageFlags.Fragment,
            module = fs.Module,
            pName = new VkUtf8ReadOnlyString("main"u8),
        };
        var shaderStages = stackalloc[] { vertShaderStageInfo, fragShaderStageInfo };

        fixed (
            VkVertexInputAttributeDescription* attributeDescriptionsPtr =
                vertexInputAttributeDescriptions
        )
        {
            VkPipelineVertexInputStateCreateInfo vertexInputInfo = new()
            {
                sType = VkStructureType.PipelineVertexInputStateCreateInfo,
                vertexBindingDescriptionCount = 1,
                pVertexBindingDescriptions = &vertexInputBindingDescription,
                vertexAttributeDescriptionCount = (uint)vertexInputAttributeDescriptions.Length,
                pVertexAttributeDescriptions = attributeDescriptionsPtr,
            };

            VkPipelineInputAssemblyStateCreateInfo inputAssembly = new()
            {
                sType = VkStructureType.PipelineInputAssemblyStateCreateInfo,
                topology = topology,
            };

            VkPipelineViewportStateCreateInfo viewportState = new()
            {
                sType = VkStructureType.PipelineViewportStateCreateInfo,
                viewportCount = 1,
                pViewports = default,
                scissorCount = 1,
                pScissors = default,
            };

            VkPipelineRasterizationStateCreateInfo rasterizer = new()
            {
                sType = VkStructureType.PipelineRasterizationStateCreateInfo,
                polygonMode = VkPolygonMode.Fill,
                cullMode = VkCullModeFlags.None,
                frontFace = VkFrontFace.CounterClockwise,
                lineWidth = 1,
            };

            VkPipelineMultisampleStateCreateInfo multisampling = new()
            {
                sType = VkStructureType.PipelineMultisampleStateCreateInfo,
                rasterizationSamples = VkSampleCountFlags.Count1,
            };

            // PipelineColorBlendAttachmentState colorBlendAttachment = new()
            // {
            //     ColorWriteMask =
            //         ColorComponentFlags.RBit
            //         | ColorComponentFlags.GBit
            //         | ColorComponentFlags.BBit
            //         | ColorComponentFlags.ABit,
            //     BlendEnable = false,
            // };
            var colorBlendAttachment = new VkPipelineColorBlendAttachmentState
            {
                blendEnable = true,
                srcColorBlendFactor = VkBlendFactor.SrcAlpha,
                dstColorBlendFactor = VkBlendFactor.OneMinusSrcAlpha,
                colorBlendOp = VkBlendOp.Add,
                srcAlphaBlendFactor = VkBlendFactor.One,
                dstAlphaBlendFactor = VkBlendFactor.OneMinusSrcAlpha,
                alphaBlendOp = VkBlendOp.Add,
                colorWriteMask =
                    VkColorComponentFlags.R
                    | VkColorComponentFlags.G
                    | VkColorComponentFlags.B
                    | VkColorComponentFlags.A,
            };

            var colorBlending = new VkPipelineColorBlendStateCreateInfo
            {
                sType = VkStructureType.PipelineColorBlendStateCreateInfo,
                attachmentCount = 1,
                pAttachments = (VkPipelineColorBlendAttachmentState*)
                    Unsafe.AsPointer(ref colorBlendAttachment),
            };
            // PipelineColorBlendStateCreateInfo colorBlending = new()
            // {
            //     sType = VkStructureType.PipelineColorBlendStateCreateInfo,
            //     LogicOpEnable = false,
            //     LogicOp = LogicOp.Copy,
            //     AttachmentCount = 1,
            //     PAttachments = &colorBlendAttachment,
            // };
            // colorBlending.BlendConstants[0] = 0;
            // colorBlending.BlendConstants[1] = 0;
            // colorBlending.BlendConstants[2] = 0;
            // colorBlending.BlendConstants[3] = 0;

            var dynamicStates = stackalloc[] { VkDynamicState.Viewport, VkDynamicState.Scissor };
            VkPipelineDynamicStateCreateInfo dynamicState = new()
            {
                sType = VkStructureType.PipelineDynamicStateCreateInfo,
                dynamicStateCount = 2,
                pDynamicStates = dynamicStates,
            };

            VkGraphicsPipelineCreateInfo pipelineInfo = new()
            {
                sType = VkStructureType.GraphicsPipelineCreateInfo,
                stageCount = 2,
                pStages = shaderStages,
                pVertexInputState = &vertexInputInfo,
                pInputAssemblyState = &inputAssembly,
                pViewportState = &viewportState,
                pRasterizationState = &rasterizer,
                pMultisampleState = &multisampling,
                pDepthStencilState = &depthStencil,
                pColorBlendState = &colorBlending,
                pDynamicState = &dynamicState,
                layout = PipelieLayout,
                subpass = 0,
                basePipelineHandle = default,
            };
            var pipelineRenderingCreate = new VkPipelineRenderingCreateInfo
            {
                sType = VkStructureType.PipelineRenderingCreateInfo,
                colorAttachmentCount = 1,
                pColorAttachmentFormats = &colorFormat,
                depthAttachmentFormat = depthFormat,
                stencilAttachmentFormat = depthFormat,
            };
            {
                // vulkan-1.3 dynamic rendering(without RenderPass and FrameBuffer)
                pipelineInfo.pNext = &pipelineRenderingCreate;
            }
            vkd.vkCreateGraphicsPipeline(pipelineInfo, out _graphicsPipeline).ThrowIfError();
        }
    }

    public unsafe void Dispose()
    {
        foreach (var pool in DescriptorPools)
        {
            pool.Dispose();
        }
        _vkd.vkDestroyPipeline(_graphicsPipeline, null);
        _vkd.vkDestroyPipelineLayout(PipelieLayout, null);
        _vkd.vkDestroyDescriptorSetLayout(DescriptorSetLayout, null);
    }

    public VkDescriptorSet Bind(
        uint frameCount,
        VkCommandBuffer commandBuffer,
        VkExtent2D extent,
        uint imageIndex
    )
    {
        var descSet = DescriptorPools[imageIndex].Get(frameCount);
        Bind(commandBuffer, extent, descSet);
        return descSet;
    }

    public unsafe void Bind(
        VkCommandBuffer commandBuffer,
        VkExtent2D extent,
        VkDescriptorSet descriptorSet
    )
    {
        VkViewport viewport = new()
        {
            x = 0,
            y = 0,
            width = extent.width,
            height = extent.height,
            minDepth = 0,
            maxDepth = 1,
        };
        _vkd.vkCmdSetViewport(commandBuffer, 0, 1, &viewport);

        VkRect2D scissor = new() { offset = { x = 0, y = 0 }, extent = extent };
        _vkd.vkCmdSetScissor(commandBuffer, 0, 1, &scissor);

        _vkd.vkCmdBindPipeline(commandBuffer, VkPipelineBindPoint.Graphics, _graphicsPipeline);

        _vkd.vkCmdBindDescriptorSets(
            commandBuffer,
            VkPipelineBindPoint.Graphics,
            PipelieLayout,
            0,
            1,
            &descriptorSet,
            0,
            null
        );
    }

    public unsafe void PushConstant(VkCommandBuffer commandBuffer, CONSTANT value)
    {
        _vkd.vkCmdPushConstants(
            commandBuffer,
            PipelieLayout,
            VkShaderStageFlags.Vertex,
            0,
            (uint)Marshal.SizeOf<CONSTANT>(),
            &value
        );
    }
}
