using Vortice.Vulkan;
using static Vortice.Vulkan.Vulkan;

namespace VrmImpl;

class VulkanPipelineObject : IDisposable
{
    static unsafe VkShaderModule createShaderModule(VkDeviceApi vkd, ReadOnlySpan<byte> code)
    {
        fixed (byte* pCode = code)
        {
            var createInfo = new VkShaderModuleCreateInfo
            {
                sType = VK_STRUCTURE_TYPE_SHADER_MODULE_CREATE_INFO,
                codeSize = (uint)code.Length,
                pCode = (uint*)pCode,
            };

            if (vkd.vkCreateShaderModule(&createInfo, null, out var shaderModule) != VK_SUCCESS)
            {
                throw new Exception("failed to create shader module!");
            }

            return shaderModule;
        }
    }

    VkDeviceApi _vkd;

    private readonly VkPipelineLayout pipelineLayout;

    private readonly VkPipeline graphicsPipeline;

    public unsafe VulkanPipelineObject(VkDeviceApi vkd, VkFormat format, VkRenderPass? renderPass)
    {
        _vkd = vkd;

        var vertShaderCode = ProjectResource.FromAssembly("shader.vert.spv");
        var vertShaderModule = createShaderModule(vkd, vertShaderCode);

        var fragShaderCode = ProjectResource.FromAssembly("shader.frag.spv");
        var fragShaderModule = createShaderModule(vkd, fragShaderCode);

        fixed (byte* main = "main"u8)
        {
            var vertShaderStageInfo = new VkPipelineShaderStageCreateInfo
            {
                sType = VK_STRUCTURE_TYPE_PIPELINE_SHADER_STAGE_CREATE_INFO,
                stage = VkShaderStageFlags.Vertex,
                module = vertShaderModule,
                pName = main,
            };
            var fragShaderStageInfo = new VkPipelineShaderStageCreateInfo
            {
                sType = VK_STRUCTURE_TYPE_PIPELINE_SHADER_STAGE_CREATE_INFO,
                stage = VkShaderStageFlags.Fragment,
                module = fragShaderModule,
                pName = main,
            };
            var shaderStages = stackalloc VkPipelineShaderStageCreateInfo[]
            {
                vertShaderStageInfo,
                fragShaderStageInfo,
            };

            var vertexInputInfo = new VkPipelineVertexInputStateCreateInfo
            {
                sType = VK_STRUCTURE_TYPE_PIPELINE_VERTEX_INPUT_STATE_CREATE_INFO,
                vertexBindingDescriptionCount = 0,
                vertexAttributeDescriptionCount = 0,
            };

            var inputAssembly = new VkPipelineInputAssemblyStateCreateInfo
            {
                sType = VK_STRUCTURE_TYPE_PIPELINE_INPUT_ASSEMBLY_STATE_CREATE_INFO,
                topology = VkPrimitiveTopology.TriangleList,
                primitiveRestartEnable = false,
            };
            var viewportState = new VkPipelineViewportStateCreateInfo
            {
                sType = VK_STRUCTURE_TYPE_PIPELINE_VIEWPORT_STATE_CREATE_INFO,
                viewportCount = 1,
                scissorCount = 1,
            };
            var rasterizer = new VkPipelineRasterizationStateCreateInfo
            {
                sType = VK_STRUCTURE_TYPE_PIPELINE_RASTERIZATION_STATE_CREATE_INFO,
                depthClampEnable = false,
                rasterizerDiscardEnable = false,
                polygonMode = VkPolygonMode.Fill,
                lineWidth = 1.0f,
                cullMode = VkCullModeFlags.Back,
                frontFace = VkFrontFace.Clockwise,
                depthBiasEnable = false,
            };
            var multisampling = new VkPipelineMultisampleStateCreateInfo
            {
                sType = VK_STRUCTURE_TYPE_PIPELINE_MULTISAMPLE_STATE_CREATE_INFO,
                sampleShadingEnable = false,
                rasterizationSamples = VkSampleCountFlags.Count1,
            };
            var colorBlendAttachment = new VkPipelineColorBlendAttachmentState
            {
                colorWriteMask =
                    VkColorComponentFlags.R
                    | VkColorComponentFlags.G
                    | VkColorComponentFlags.B
                    | VkColorComponentFlags.A,
                blendEnable = false,
            };
            var colorBlending = new VkPipelineColorBlendStateCreateInfo
            {
                sType = VK_STRUCTURE_TYPE_PIPELINE_COLOR_BLEND_STATE_CREATE_INFO,
                logicOpEnable = false,
                logicOp = VkLogicOp.Copy,
                attachmentCount = 1,
                pAttachments = &colorBlendAttachment,
            };
            colorBlending.blendConstants[0] = 0.0f;
            colorBlending.blendConstants[1] = 0.0f;
            colorBlending.blendConstants[2] = 0.0f;
            colorBlending.blendConstants[3] = 0.0f;

            var dynamicStates = stackalloc VkDynamicState[]
            {
                VkDynamicState.Viewport,
                VkDynamicState.Scissor,
            };
            var dynamicState = new VkPipelineDynamicStateCreateInfo
            {
                sType = VK_STRUCTURE_TYPE_PIPELINE_DYNAMIC_STATE_CREATE_INFO,
                dynamicStateCount = 2, //(uint)dynamicStates.Length,
                pDynamicStates = dynamicStates,
            };

            var pipelineLayoutInfo = new VkPipelineLayoutCreateInfo
            {
                sType = VK_STRUCTURE_TYPE_PIPELINE_LAYOUT_CREATE_INFO,
                setLayoutCount = 0,
                pushConstantRangeCount = 0,
            };

            if (
                _vkd.vkCreatePipelineLayout(&pipelineLayoutInfo, null, out pipelineLayout)
                != VK_SUCCESS
            )
            {
                throw new Exception("failed to create pipeline layout!");
            }

            var pipelineInfo = new VkGraphicsPipelineCreateInfo
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
                layout = pipelineLayout,
                // renderPass = renderPasss,
                subpass = 0,
                basePipelineHandle = default,
            };
            if (renderPass is VkRenderPass rp)
            {
                pipelineInfo.renderPass = rp;
            }
            else
            {
                var renderingCI = new VkPipelineRenderingCreateInfo
                {
                    sType = VK_STRUCTURE_TYPE_PIPELINE_RENDERING_CREATE_INFO,
                    colorAttachmentCount = 1,
                    pColorAttachmentFormats = &format,
                    // depthAttachmentFormat = depthFormat
                };
                pipelineInfo.pNext = &renderingCI;
            }

            VkPipeline _graphicsPipeline;
            if (
                _vkd.vkCreateGraphicsPipelines(default, 1, &pipelineInfo, null, &_graphicsPipeline)
                != VK_SUCCESS
            )
            {
                throw new Exception("failed to create graphics pipeline!");
            }
            graphicsPipeline = _graphicsPipeline;

            _vkd.vkDestroyShaderModule(fragShaderModule, null);
            _vkd.vkDestroyShaderModule(vertShaderModule, null);
        }
    }

    public unsafe void Dispose()
    {
        _vkd.vkDestroyPipeline(graphicsPipeline, null);
        _vkd.vkDestroyPipelineLayout(pipelineLayout, null);
    }

    public void RecordCommandBuffer(VkCommandBuffer commandBuffer)
    {
        _vkd.vkCmdBindPipeline(commandBuffer, VkPipelineBindPoint.Graphics, graphicsPipeline);
        _vkd.vkCmdDraw(commandBuffer, 3, 1, 0, 0);
    }
}
