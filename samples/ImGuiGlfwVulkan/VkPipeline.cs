using System.Runtime.InteropServices;
using Silk.NET.Core.Native;
using Vortice.Vulkan;
using static Vortice.Vulkan.Vulkan;

namespace VrmImpl;

public class VkPipeline<CONSTANT> : IDisposable
    where CONSTANT : unmanaged
{
    VkDeviceApi _vd;
    VkDevice _device;

    public readonly VkDescriptorSetLayout DescriptorSetLayout;
    public readonly DescriptorPoolObject[] DescriptorPools;

    public readonly VkPipelineLayout PipelieLayout;
    private readonly VkPipeline _graphicsPipeline;

    public record struct RenderPassArgs(
        VkExtent2D extent,
        VkImageView[] imageViews,
        VkImageView depthImageView
    ) { }

    public record struct DepthStencilInfo(
        VkFormat depthFormat,
        VkPipelineDepthStencilStateCreateInfo depthStencil
    ) { }

    public unsafe VkPipeline(
        VkDeviceApi vd,
        VkDevice device,
        VkShaderModule vs,
        VkShaderModule fs,
        VkPrimitiveTopology topology,
        VkVertexInputBindingDescription vertexInputBindingDescription,
        VkVertexInputAttributeDescription[] vertexInputAttributeDescriptions,
        uint maxFlightCount,
        ReadOnlySpan<VkDescriptorSetLayoutBinding> descriptorSetLayoutBindings,
        VkFormat colorFormat,
        DepthStencilInfo? depthStencil
    )
    {
        _vd = vd;
        _device = device;

        //
        // descriptorSetLayout
        //
        fixed (VkDescriptorSetLayoutBinding* bindingsPtr = descriptorSetLayoutBindings)
        {
            VkDescriptorSetLayoutCreateInfo layoutInfo = new()
            {
                sType = VK_STRUCTURE_TYPE_DESCRIPTOR_SET_LAYOUT_CREATE_INFO,
                bindingCount = (uint)descriptorSetLayoutBindings.Length,
                pBindings = bindingsPtr,
            };
            if (
                _vd.vkCreateDescriptorSetLayout(in layoutInfo, null, out DescriptorSetLayout)
                != VK_SUCCESS
            )
            {
                throw new Exception("failed to create descriptor set layout!");
            }
        }

        //
        // descriptorPool
        //
        DescriptorPools = new DescriptorPoolObject[maxFlightCount];
        for (int i = 0; i < DescriptorPools.Length; ++i)
        {
            DescriptorPools[i] = new DescriptorPoolObject(
                _vd,
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
            sType = VK_STRUCTURE_TYPE_PIPELINE_LAYOUT_CREATE_INFO,
            setLayoutCount = 1,
            pSetLayouts = &descriptorSetLayout,
            pushConstantRangeCount = 1,
            pPushConstantRanges = &constantRange,
        };
        if (
            _vd.vkCreatePipelineLayout(in pipelineLayoutInfo, null, out PipelieLayout) != VK_SUCCESS
        )
        {
            throw new Exception("failed to create pipeline layout!");
        }

        //
        VkPipelineShaderStageCreateInfo vertShaderStageInfo = new()
        {
            sType = VK_STRUCTURE_TYPE_PIPELINE_SHADER_STAGE_CREATE_INFO,
            stage = VkShaderStageFlags.Vertex,
            module = vs,
            pName = (byte*)SilkMarshal.StringToPtr("main"),
        };
        VkPipelineShaderStageCreateInfo fragShaderStageInfo = new()
        {
            sType = VK_STRUCTURE_TYPE_PIPELINE_SHADER_STAGE_CREATE_INFO,
            stage = VkShaderStageFlags.Fragment,
            module = fs,
            pName = (byte*)SilkMarshal.StringToPtr("main"),
        };
        var shaderStages = stackalloc[] { vertShaderStageInfo, fragShaderStageInfo };

        fixed (
            VkVertexInputAttributeDescription* attributeDescriptionsPtr =
                vertexInputAttributeDescriptions
        )
        {
            VkPipelineVertexInputStateCreateInfo vertexInputInfo = new()
            {
                sType = VK_STRUCTURE_TYPE_PIPELINE_VERTEX_INPUT_STATE_CREATE_INFO,
                vertexBindingDescriptionCount = 1,
                pVertexBindingDescriptions = &vertexInputBindingDescription,
                vertexAttributeDescriptionCount = (uint)vertexInputAttributeDescriptions.Length,
                pVertexAttributeDescriptions = attributeDescriptionsPtr,
            };

            VkPipelineInputAssemblyStateCreateInfo inputAssembly = new()
            {
                sType = VK_STRUCTURE_TYPE_PIPELINE_INPUT_ASSEMBLY_STATE_CREATE_INFO,
                topology = topology,
            };

            VkPipelineViewportStateCreateInfo viewportState = new()
            {
                sType = VK_STRUCTURE_TYPE_PIPELINE_VIEWPORT_STATE_CREATE_INFO,
                viewportCount = 1,
                pViewports = default,
                scissorCount = 1,
                pScissors = default,
            };

            VkPipelineRasterizationStateCreateInfo rasterizer = new()
            {
                sType = VK_STRUCTURE_TYPE_PIPELINE_RASTERIZATION_STATE_CREATE_INFO,
                polygonMode = VkPolygonMode.Fill,
                cullMode = VkCullModeFlags.None,
                frontFace = VkFrontFace.CounterClockwise,
                lineWidth = 1,
            };

            VkPipelineMultisampleStateCreateInfo multisampling = new()
            {
                sType = VK_STRUCTURE_TYPE_PIPELINE_MULTISAMPLE_STATE_CREATE_INFO,
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
                sType = VK_STRUCTURE_TYPE_PIPELINE_COLOR_BLEND_STATE_CREATE_INFO,
                attachmentCount = 1,
                pAttachments = &colorBlendAttachment,
            };
            // PipelineColorBlendStateCreateInfo colorBlending = new()
            // {
            //     SType = VK_STRUCTURE_TYPE_PipelineColorBlendStateCreateInfo,
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
                sType = VK_STRUCTURE_TYPE_PIPELINE_DYNAMIC_STATE_CREATE_INFO,
                dynamicStateCount = 2,
                pDynamicStates = dynamicStates,
            };

            VkGraphicsPipelineCreateInfo pipelineInfo = new()
            {
                sType = VK_STRUCTURE_TYPE_GRAPHICS_PIPELINE_CREATE_INFO,
                stageCount = 2,
                pStages = shaderStages,
                pVertexInputState = &vertexInputInfo,
                pInputAssemblyState = &inputAssembly,
                pViewportState = &viewportState,
                pRasterizationState = &rasterizer,
                pMultisampleState = &multisampling,
                pColorBlendState = &colorBlending,
                pDynamicState = &dynamicState,
                layout = PipelieLayout,
                subpass = 0,
                basePipelineHandle = default,
            };
            var pipelineRenderingCreate = new VkPipelineRenderingCreateInfo
            {
                sType = VK_STRUCTURE_TYPE_PIPELINE_RENDERING_CREATE_INFO,
                colorAttachmentCount = 1,
                pColorAttachmentFormats = &colorFormat,
            };
            if (
                depthStencil is
                (VkFormat depthFormat, VkPipelineDepthStencilStateCreateInfo depthStencilInfo)
            )
            {
                pipelineInfo.pDepthStencilState = &depthStencilInfo;
                pipelineRenderingCreate.depthAttachmentFormat = depthFormat;
                pipelineRenderingCreate.stencilAttachmentFormat = depthFormat;
            }
            {
                // vulkan-1.3 dynamic rendering(without RenderPass and FrameBuffer)
                pipelineInfo.pNext = &pipelineRenderingCreate;
            }
            VkPipeline graphicsPipeline;
            if (
                vd.vkCreateGraphicsPipelines(default, 1, &pipelineInfo, null, &graphicsPipeline)
                != VK_SUCCESS
            )
            {
                throw new Exception("failed to create graphics pipeline!");
            }
            _graphicsPipeline = graphicsPipeline;
        }

        SilkMarshal.Free((nint)vertShaderStageInfo.pName);
        SilkMarshal.Free((nint)fragShaderStageInfo.pName);
    }

    public unsafe void Dispose()
    {
        foreach (var pool in DescriptorPools)
        {
            pool.Dispose();
        }
        _vd.vkDestroyPipeline(_graphicsPipeline, null);
        _vd.vkDestroyPipelineLayout(PipelieLayout, null);
        _vd.vkDestroyDescriptorSetLayout(DescriptorSetLayout, null);
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
        _vd.vkCmdSetViewport(commandBuffer, 0, 1, &viewport);

        VkRect2D scissor = new() { offset = { x = 0, y = 0 }, extent = extent };
        _vd.vkCmdSetScissor(commandBuffer, 0, 1, &scissor);

        _vd.vkCmdBindPipeline(commandBuffer, VkPipelineBindPoint.Graphics, _graphicsPipeline);

        _vd.vkCmdBindDescriptorSets(
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
        _vd.vkCmdPushConstants(
            commandBuffer,
            PipelieLayout,
            VkShaderStageFlags.Vertex,
            0,
            (uint)Marshal.SizeOf<CONSTANT>(),
            &value
        );
    }
}
