using Vortice.Vulkan;
using static Vortice.Vulkan.Vulkan;

class VulkanRenderTarget : IDisposable
{
    private readonly VkDeviceApi _vkd;
    public readonly VkRenderPass RenderPass;

    private readonly VkImageView[] _imageViews;
    private readonly VkFramebuffer[] _framebuffers;

    private VkCommandPool _commandPool;
    private VkCommandBuffer _commandBuffer;
    private readonly VkQueue _graphicsQueue;

    public unsafe VulkanRenderTarget(
        VkDeviceApi vkd,
        uint graphicsFamily,
        VkFormat format,
        VkExtent2D extent,
        VkImage[] images
    )
    {
        _vkd = vkd;
        vkd.vkGetDeviceQueue(graphicsFamily, 0, out _graphicsQueue);

        {
            _imageViews = new VkImageView[images.Length];
            for (int i = 0; i < images.Length; ++i)
            {
                var createInfo = new VkImageViewCreateInfo
                {
                    sType = VK_STRUCTURE_TYPE_IMAGE_VIEW_CREATE_INFO,
                    image = images[i],
                    viewType = VkImageViewType.Image2D,
                    format = format,
                };
                createInfo.components.r = VkComponentSwizzle.Identity;
                createInfo.components.g = VkComponentSwizzle.Identity;
                createInfo.components.b = VkComponentSwizzle.Identity;
                createInfo.components.a = VkComponentSwizzle.Identity;
                createInfo.subresourceRange.aspectMask = VkImageAspectFlags.Color;
                createInfo.subresourceRange.baseMipLevel = 0;
                createInfo.subresourceRange.levelCount = 1;
                createInfo.subresourceRange.baseArrayLayer = 0;
                createInfo.subresourceRange.layerCount = 1;

                if (_vkd.vkCreateImageView(&createInfo, null, out _imageViews[i]) != VK_SUCCESS)
                {
                    throw new Exception("failed to create image views!");
                }
            }
        }
        {
            var colorAttachment = new VkAttachmentDescription
            {
                format = format,
                samples = VkSampleCountFlags.Count1,
                loadOp = VkAttachmentLoadOp.Clear,
                storeOp = VkAttachmentStoreOp.Store,
                stencilLoadOp = VkAttachmentLoadOp.DontCare,
                stencilStoreOp = VkAttachmentStoreOp.DontCare,
                initialLayout = VkImageLayout.Undefined,
                finalLayout = VkImageLayout.PresentSrcKHR,
            };

            var colorAttachmentRef = new VkAttachmentReference
            {
                attachment = 0,
                layout = VkImageLayout.ColorAttachmentOptimal,
            };

            var subpass = new VkSubpassDescription
            {
                pipelineBindPoint = VkPipelineBindPoint.Graphics,
                colorAttachmentCount = 1,
                pColorAttachments = &colorAttachmentRef,
            };

            var dependency = new VkSubpassDependency
            {
                srcSubpass = VK_SUBPASS_EXTERNAL,
                dstSubpass = 0,
                srcStageMask = VkPipelineStageFlags.ColorAttachmentOutput,
                srcAccessMask = 0,
                dstStageMask = VkPipelineStageFlags.ColorAttachmentOutput,
                dstAccessMask = VkAccessFlags.ColorAttachmentWrite,
            };

            var renderPassInfo = new VkRenderPassCreateInfo
            {
                sType = VK_STRUCTURE_TYPE_RENDER_PASS_CREATE_INFO,
                attachmentCount = 1,
                pAttachments = &colorAttachment,
                subpassCount = 1,
                pSubpasses = &subpass,
                dependencyCount = 1,
                pDependencies = &dependency,
            };

            if (_vkd.vkCreateRenderPass(&renderPassInfo, null, out RenderPass) != VK_SUCCESS)
            {
                throw new Exception("failed to create render pass!");
            }
        }
        {
            _framebuffers = new VkFramebuffer[_imageViews.Length];

            for (int i = 0; i < _imageViews.Length; i++)
            {
                var attachment = _imageViews[i];

                var framebufferInfo = new VkFramebufferCreateInfo
                {
                    sType = VK_STRUCTURE_TYPE_FRAMEBUFFER_CREATE_INFO,
                    renderPass = RenderPass,
                    attachmentCount = 1,
                    pAttachments = &attachment,
                    width = extent.width,
                    height = extent.height,
                    layers = 1,
                };

                if (
                    _vkd.vkCreateFramebuffer(&framebufferInfo, null, out _framebuffers[i])
                    != VK_SUCCESS
                )
                {
                    throw new Exception("failed to create framebuffer!");
                }
            }
        }

        var poolInfo = new VkCommandPoolCreateInfo
        {
            sType = VK_STRUCTURE_TYPE_COMMAND_POOL_CREATE_INFO,
            flags = VkCommandPoolCreateFlags.ResetCommandBuffer,
            queueFamilyIndex = graphicsFamily,
        };

        if (_vkd.vkCreateCommandPool(&poolInfo, null, out _commandPool) != VK_SUCCESS)
        {
            throw new Exception("failed to create command pool!");
        }

        var allocInfo = new VkCommandBufferAllocateInfo
        {
            sType = VK_STRUCTURE_TYPE_COMMAND_BUFFER_ALLOCATE_INFO,
            commandPool = _commandPool,
            level = VkCommandBufferLevel.Primary,
            commandBufferCount = 1,
        };

        VkCommandBuffer _commandBuffer;
        if (_vkd.vkAllocateCommandBuffers(&allocInfo, &_commandBuffer) != VK_SUCCESS)
        {
            throw new Exception("failed to allocate command buffers!");
        }
        this._commandBuffer = _commandBuffer;
    }

    public unsafe void Dispose()
    {
        _vkd.vkDestroyCommandPool(_commandPool, null);

        foreach (var framebuffer in _framebuffers)
        {
            _vkd.vkDestroyFramebuffer(framebuffer, null);
        }
        foreach (var imageView in _imageViews)
        {
            _vkd.vkDestroyImageView(imageView, null);
        }
        _vkd.vkDestroyRenderPass(RenderPass, null);
    }

    public unsafe void vkEndSubmitCommandBuffer(
        VkSemaphore imageAvailableSemaphore,
        VkSemaphore renderFinishedSemaphore,
        VkFence inFlightFence
    )
    {
        if (_vkd.vkEndCommandBuffer(_commandBuffer) != VK_SUCCESS)
        {
            throw new Exception("failed to record command buffer!");
        }

        var waitSemaphores = stackalloc VkSemaphore[] { imageAvailableSemaphore };
        var waitStages = stackalloc VkPipelineStageFlags[]
        {
            VkPipelineStageFlags.ColorAttachmentOutput,
        };
        var signalSemaphores = stackalloc VkSemaphore[] { renderFinishedSemaphore };
        var cmd = _commandBuffer;
        var submitInfo = new VkSubmitInfo
        {
            sType = VK_STRUCTURE_TYPE_SUBMIT_INFO,
            waitSemaphoreCount = 1,
            pWaitSemaphores = waitSemaphores,
            pWaitDstStageMask = waitStages,
            commandBufferCount = 1,
            pCommandBuffers = &cmd,
            signalSemaphoreCount = 1,
            pSignalSemaphores = signalSemaphores,
        };
        if (_vkd.vkQueueSubmit(_graphicsQueue, 1, &submitInfo, inFlightFence) != VK_SUCCESS)
        {
            throw new Exception("failed to submit draw command buffer!");
        }
    }

    public unsafe VkCommandBuffer BeginRenderPass(
        uint imageIndex,
        VkExtent2D extent,
        ReadOnlySpan<VkClearValue> clearValues
    )
    {
        _vkd.vkResetCommandBuffer(
            _commandBuffer, /*VkCommandBufferResetFlagBits*/
            0
        );

        var beginInfo = new VkCommandBufferBeginInfo
        {
            sType = VK_STRUCTURE_TYPE_COMMAND_BUFFER_BEGIN_INFO,
        };
        if (_vkd.vkBeginCommandBuffer(_commandBuffer, &beginInfo) != VK_SUCCESS)
        {
            throw new Exception("failed to begin recording command buffer!");
        }

        var renderPassInfo = new VkRenderPassBeginInfo
        {
            sType = VK_STRUCTURE_TYPE_RENDER_PASS_BEGIN_INFO,
            renderPass = RenderPass,
            framebuffer = _framebuffers[imageIndex],
        };
        renderPassInfo.renderArea.offset = new(0, 0);
        renderPassInfo.renderArea.extent = extent;

        fixed (VkClearValue* pClearValues = clearValues)
        {
            renderPassInfo.clearValueCount = (uint)clearValues.Length;
            renderPassInfo.pClearValues = pClearValues;
            _vkd.vkCmdBeginRenderPass(_commandBuffer, &renderPassInfo, VkSubpassContents.Inline);
        }

        var viewport = new VkViewport
        {
            x = 0.0f,
            y = 0.0f,
            width = extent.width,
            height = extent.height,
            minDepth = 0.0f,
            maxDepth = 1.0f,
        };
        _vkd.vkCmdSetViewport(_commandBuffer, 0, 1, &viewport);

        var scissor = new VkRect2D { offset = new(0, 0), extent = extent };
        _vkd.vkCmdSetScissor(_commandBuffer, 0, 1, &scissor);

        return _commandBuffer;
    }

    public void EndRenderPass()
    {
        _vkd.vkCmdEndRenderPass(_commandBuffer);
    }

    public unsafe VkCommandBuffer BeginRendering(
        uint imageIndex,
        VkImage image,
        VkExtent2D extent,
        ReadOnlySpan<VkClearValue> clearValues
    )
    {
        _vkd.vkResetCommandBuffer(
            _commandBuffer, /*VkCommandBufferResetFlagBits*/
            0
        );

        var beginInfo = new VkCommandBufferBeginInfo
        {
            sType = VK_STRUCTURE_TYPE_COMMAND_BUFFER_BEGIN_INFO,
        };
        if (_vkd.vkBeginCommandBuffer(_commandBuffer, &beginInfo) != VK_SUCCESS)
        {
            throw new Exception("failed to begin recording command buffer!");
        }

        // TransitionImageLayout(
        //     _vkd,
        //     _commandBuffer,
        //     image,
        //     VkImageLayout.ColorAttachmentOptimal
        // );
        var b = new VkImageMemoryBarrier2
        {
            sType = VK_STRUCTURE_TYPE_IMAGE_MEMORY_BARRIER_2,
            srcStageMask = VK_PIPELINE_STAGE_2_COLOR_ATTACHMENT_OUTPUT_BIT,
            srcAccessMask = 0,
            dstStageMask = VK_PIPELINE_STAGE_2_COLOR_ATTACHMENT_OUTPUT_BIT,
            dstAccessMask = (VkAccessFlags2)(
                VK_ACCESS_COLOR_ATTACHMENT_READ_BIT | VK_ACCESS_COLOR_ATTACHMENT_WRITE_BIT
            ),
            oldLayout = VK_IMAGE_LAYOUT_UNDEFINED,
            newLayout = VK_IMAGE_LAYOUT_ATTACHMENT_OPTIMAL,
            image = image,
            subresourceRange = new()
            {
                aspectMask = VK_IMAGE_ASPECT_COLOR_BIT,
                levelCount = 1,
                layerCount = 1,
            },
        };
        var barrierDependencyInfo = new VkDependencyInfo
        {
            sType = VK_STRUCTURE_TYPE_DEPENDENCY_INFO,
            imageMemoryBarrierCount = 1,
            pImageMemoryBarriers = &b,
        };
        _vkd.vkCmdPipelineBarrier2(_commandBuffer, &barrierDependencyInfo);

        var color_attachment_info = new VkRenderingAttachmentInfo
        {
            sType = VK_STRUCTURE_TYPE_RENDERING_ATTACHMENT_INFO,
            imageView = _imageViews[imageIndex],
            imageLayout = VkImageLayout.ColorAttachmentOptimal,
            loadOp = VkAttachmentLoadOp.Clear,
            storeOp = VkAttachmentStoreOp.Store,
            clearValue = clearValues[0],
        };
        // var depth_attachment_info = new RenderingAttachmentInfo()
        // {
        //     SType = VK_STRUCTURE_TYPE_RenderingAttachmentInfo,
        //     ImageView = DepthImageView,
        //     ImageLayout = ImageLayout.DepthAttachmentOptimal,
        //     LoadOp = depthLoadOp,
        //     StoreOp = depthStoreOp,
        //     ClearValue = new ClearValue { DepthStencil = clearDepthStencil },
        // };
        var render_info = new VkRenderingInfo
        {
            sType = VK_STRUCTURE_TYPE_RENDERING_INFO,
            renderArea = new() { extent = extent },
            layerCount = 1,
            colorAttachmentCount = 1,
            pColorAttachments = &color_attachment_info,
            // PDepthAttachment = &depth_attachment_info,
            // PStencilAttachment = &depth_attachment_info,
        };

        _vkd.vkCmdBeginRendering(_commandBuffer, &render_info);

        var viewport = new VkViewport
        {
            x = 0.0f,
            y = 0.0f,
            width = extent.width,
            height = extent.height,
            minDepth = 0.0f,
            maxDepth = 1.0f,
        };
        _vkd.vkCmdSetViewport(_commandBuffer, 0, 1, &viewport);

        var scissor = new VkRect2D { offset = new(0, 0), extent = extent };
        _vkd.vkCmdSetScissor(_commandBuffer, 0, 1, &scissor);

        return _commandBuffer;
    }

    public unsafe void EndRendering(VkImage image)
    {
        _vkd.vkCmdEndRendering(_commandBuffer);
        // TransitionImageLayout(_vkd, _commandBuffer, image, VkImageLayout.PresentSrcKHR);

        var barrierPresent = new VkImageMemoryBarrier2
        {
            sType = VK_STRUCTURE_TYPE_IMAGE_MEMORY_BARRIER_2,
            srcStageMask = VK_PIPELINE_STAGE_2_COLOR_ATTACHMENT_OUTPUT_BIT,
            srcAccessMask = (VkAccessFlags2)VK_ACCESS_COLOR_ATTACHMENT_WRITE_BIT,
            dstStageMask = VK_PIPELINE_STAGE_2_COLOR_ATTACHMENT_OUTPUT_BIT,
            dstAccessMask = 0,
            oldLayout = VK_IMAGE_LAYOUT_ATTACHMENT_OPTIMAL,
            newLayout = VK_IMAGE_LAYOUT_PRESENT_SRC_KHR,
            image = image,
            subresourceRange = new()
            {
                aspectMask = VK_IMAGE_ASPECT_COLOR_BIT,
                levelCount = 1,
                layerCount = 1,
            },
        };
        var barrierPresentDependencyInfo = new VkDependencyInfo
        {
            sType = VK_STRUCTURE_TYPE_DEPENDENCY_INFO,
            imageMemoryBarrierCount = 1,
            pImageMemoryBarriers = &barrierPresent,
        };
        _vkd.vkCmdPipelineBarrier2(_commandBuffer, &barrierPresentDependencyInfo);
    }

    public static unsafe void TransitionImageLayout(
        VkDeviceApi vd,
        VkCommandBuffer commandBuffer,
        VkImage image,
        VkImageLayout newLayout
    )
    {
        VkImageMemoryBarrier barrier = new()
        {
            sType = VK_STRUCTURE_TYPE_IMAGE_MEMORY_BARRIER,
            oldLayout = VkImageLayout.Undefined,
            newLayout = newLayout,
            srcQueueFamilyIndex = VK_QUEUE_FAMILY_IGNORED,
            dstQueueFamilyIndex = VK_QUEUE_FAMILY_IGNORED,
            image = image,
            subresourceRange =
            {
                aspectMask = VkImageAspectFlags.Color,
                baseMipLevel = 0,
                levelCount = 1,
                baseArrayLayer = 0,
                layerCount = 1,
            },
        };

        vd.vkCmdPipelineBarrier(
            commandBuffer,
            VkPipelineStageFlags.BottomOfPipe,
            VkPipelineStageFlags.TopOfPipe,
            0,
            0,
            null,
            0,
            null,
            1,
            &barrier
        );
    }
}
