// https://github.com/Overv/VulkanTutorial/blob/main/code/15_hello_triangle.cpp

using Vortice.Vulkan;
using static Vortice.Vulkan.Vulkan;

namespace VrmImpl.VorticeVulkan;

public class VulkanRenderTargetObject : IDisposable
{
    private readonly VkDeviceApi _vkd;
    private readonly VkQueue _graphicsQueue;
    private VkExtent2D _extent;

    private VkRenderPass? _renderPass;

    private VkImage[] _images;
    private VkImageView[] _imageViews = [];
    private VkFramebuffer[] _framebuffers = [];
    private readonly uint _graphicsQueueFamilyIndex;
    private VkCommandPool? _commandPool;
    private VkCommandBuffer[] _commandBuffers;

    public VulkanRenderTargetObject(
        VkDeviceApi vkd,
        uint graphicsQueueFamilyIndex,
        VkFormat format,
        VkExtent2D extent,
        VkImage[] images
    )
    {
        _vkd = vkd;
        _graphicsQueueFamilyIndex = graphicsQueueFamilyIndex;
        vkd.vkGetDeviceQueue(graphicsQueueFamilyIndex, 0, out _graphicsQueue);

        Create(format, extent, images);
    }

    public void Create(VkFormat format, VkExtent2D extent, VkImage[] images)
    {
        Dispose();
        _extent = extent;
        _images = images;
        (_imageViews, _renderPass, _framebuffers, _commandPool, _commandBuffers) = _Create(
            _vkd,
            format,
            extent,
            images,
            _graphicsQueueFamilyIndex
        );
    }

    private static unsafe (
        VkImageView[],
        VkRenderPass,
        VkFramebuffer[],
        VkCommandPool,
        VkCommandBuffer[]
    ) _Create(
        VkDeviceApi vkd,
        VkFormat format,
        VkExtent2D extent,
        VkImage[] images,
        uint graphicsQueueFamilyIndex
    )
    {
        var imageViews = new VkImageView[images.Length];
        {
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
                vkd.vkCreateImageView(&createInfo, null, out imageViews[i]).ThrowIfError();
            }
        }

        VkRenderPass renderPass;
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

            vkd.vkCreateRenderPass(&renderPassInfo, null, out renderPass).ThrowIfError();
        }

        var framebuffers = new VkFramebuffer[imageViews.Length];
        {
            for (int i = 0; i < imageViews.Length; i++)
            {
                var attachment = imageViews[i];

                var framebufferInfo = new VkFramebufferCreateInfo
                {
                    sType = VK_STRUCTURE_TYPE_FRAMEBUFFER_CREATE_INFO,
                    renderPass = renderPass,
                    attachmentCount = 1,
                    pAttachments = &attachment,
                    width = extent.width,
                    height = extent.height,
                    layers = 1,
                };

                vkd.vkCreateFramebuffer(&framebufferInfo, null, out framebuffers[i]).ThrowIfError();
            }
        }

        var poolInfo = new VkCommandPoolCreateInfo
        {
            sType = VK_STRUCTURE_TYPE_COMMAND_POOL_CREATE_INFO,
            flags = VkCommandPoolCreateFlags.ResetCommandBuffer,
            queueFamilyIndex = graphicsQueueFamilyIndex,
        };
        vkd.vkCreateCommandPool(&poolInfo, null, out var commandPool).ThrowIfError();

        var allocInfo = new VkCommandBufferAllocateInfo
        {
            sType = VK_STRUCTURE_TYPE_COMMAND_BUFFER_ALLOCATE_INFO,
            commandPool = commandPool,
            level = VkCommandBufferLevel.Primary,
            commandBufferCount = (uint)images.Length,
        };
        var commandBuffers = stackalloc VkCommandBuffer[images.Length];
        vkd.vkAllocateCommandBuffers(&allocInfo, commandBuffers).ThrowIfError();

        return (
            imageViews,
            renderPass,
            framebuffers,
            commandPool,
            new ReadOnlySpan<VkCommandBuffer>(commandBuffers, images.Length).ToArray()
        );
    }

    public unsafe void Dispose()
    {
        if (_commandPool is VkCommandPool commandPool)
        {
            _vkd.vkDestroyCommandPool(commandPool, null);
            _commandPool = null;
        }
        foreach (var framebuffer in _framebuffers)
        {
            _vkd.vkDestroyFramebuffer(framebuffer, null);
        }
        _framebuffers = [];
        foreach (var imageView in _imageViews)
        {
            _vkd.vkDestroyImageView(imageView, null);
        }
        _imageViews = [];
        if (_renderPass is VkRenderPass renderPass)
        {
            _vkd.vkDestroyRenderPass(renderPass, null);
            _renderPass = null;
        }
    }

    public unsafe void EndSubmitCommandBuffer(
        uint imageIndex,
        ReadOnlySpan<VkSemaphore> waitSemaphores,
        VkSemaphore renderFinishedSemaphore,
        VkFence inFlightFence
    )
    {
        var commandBuffer = _commandBuffers[imageIndex];
        if (_vkd.vkEndCommandBuffer(commandBuffer) != VK_SUCCESS)
        {
            throw new Exception("failed to record command buffer!");
        }

        // var waitSemaphores = stackalloc VkSemaphore[] { imageAvailableSemaphore };
        var waitStages = stackalloc VkPipelineStageFlags[]
        {
            VkPipelineStageFlags.ColorAttachmentOutput,
        };
        var signalSemaphores = stackalloc VkSemaphore[] { renderFinishedSemaphore };
        fixed (VkSemaphore* pWaitSemaphores = waitSemaphores)
        {
            var submitInfo = new VkSubmitInfo
            {
                sType = VK_STRUCTURE_TYPE_SUBMIT_INFO,
                waitSemaphoreCount = (uint)waitSemaphores.Length,
                pWaitSemaphores = pWaitSemaphores,
                pWaitDstStageMask = waitStages,
                commandBufferCount = 1,
                pCommandBuffers = &commandBuffer,
                signalSemaphoreCount = 1,
                pSignalSemaphores = signalSemaphores,
            };
            if (_vkd.vkQueueSubmit(_graphicsQueue, 1, &submitInfo, inFlightFence) != VK_SUCCESS)
            {
                throw new Exception("failed to submit draw command buffer!");
            }
        }
    }

    public unsafe VkCommandBuffer BeginRenderPass(
        uint imageIndex,
        ReadOnlySpan<VkClearValue> clearValues
    )
    {
        var commandBuffer = _commandBuffers[imageIndex];
        _vkd.vkResetCommandBuffer(
            _commandBuffers[imageIndex], /*VkCommandBufferResetFlagBits*/
            0
        );

        var beginInfo = new VkCommandBufferBeginInfo
        {
            sType = VK_STRUCTURE_TYPE_COMMAND_BUFFER_BEGIN_INFO,
        };
        if (_vkd.vkBeginCommandBuffer(commandBuffer, &beginInfo) != VK_SUCCESS)
        {
            throw new Exception("failed to begin recording command buffer!");
        }

        var renderPassInfo = new VkRenderPassBeginInfo
        {
            sType = VK_STRUCTURE_TYPE_RENDER_PASS_BEGIN_INFO,
            framebuffer = _framebuffers[imageIndex],
        };
        if (_renderPass is VkRenderPass renderPass)
        {
            renderPassInfo.renderPass = renderPass;
        }
        renderPassInfo.renderArea.offset = new(0, 0);
        renderPassInfo.renderArea.extent = _extent;

        fixed (VkClearValue* pClearValues = clearValues)
        {
            renderPassInfo.clearValueCount = (uint)clearValues.Length;
            renderPassInfo.pClearValues = pClearValues;
            _vkd.vkCmdBeginRenderPass(commandBuffer, &renderPassInfo, VkSubpassContents.Inline);
        }

        VkViewport viewport = new()
        {
            x = 0.0f,
            y = 0.0f,
            width = _extent.width,
            height = _extent.height,
            minDepth = 0.0f,
            maxDepth = 1.0f,
        };
        _vkd.vkCmdSetViewport(commandBuffer, 0, 1, &viewport);

        var scissor = new VkRect2D { offset = new(0, 0), extent = _extent };
        _vkd.vkCmdSetScissor(commandBuffer, 0, 1, &scissor);

        return commandBuffer;
    }

    public void EndRenderPass(uint imageIndex)
    {
        _vkd.vkCmdEndRenderPass(_commandBuffers[imageIndex]);
    }

    public unsafe VkCommandBuffer BeginRendering(
        uint imageIndex,
        ReadOnlySpan<VkClearValue> clearValues
    )
    {
        var commandBuffer = _commandBuffers[imageIndex];
        _vkd.vkResetCommandBuffer(
            commandBuffer, /*VkCommandBufferResetFlagBits*/
            0
        );

        var beginInfo = new VkCommandBufferBeginInfo
        {
            sType = VK_STRUCTURE_TYPE_COMMAND_BUFFER_BEGIN_INFO,
        };
        if (_vkd.vkBeginCommandBuffer(commandBuffer, &beginInfo) != VK_SUCCESS)
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
            image = _images[imageIndex],
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
        _vkd.vkCmdPipelineBarrier2(commandBuffer, &barrierDependencyInfo);

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
            renderArea = new() { extent = _extent },
            layerCount = 1,
            colorAttachmentCount = 1,
            pColorAttachments = &color_attachment_info,
            // PDepthAttachment = &depth_attachment_info,
            // PStencilAttachment = &depth_attachment_info,
        };

        _vkd.vkCmdBeginRendering(commandBuffer, &render_info);

        VkViewport viewport = new()
        {
            x = 0.0f,
            y = 0.0f,
            width = _extent.width,
            height = _extent.height,
            minDepth = 0.0f,
            maxDepth = 1.0f,
        };
        _vkd.vkCmdSetViewport(commandBuffer, 0, 1, &viewport);

        var scissor = new VkRect2D { offset = new(0, 0), extent = _extent };
        _vkd.vkCmdSetScissor(commandBuffer, 0, 1, &scissor);

        return commandBuffer;
    }

    public unsafe void EndRendering(uint imageIndex)
    {
        _vkd.vkCmdEndRendering(_commandBuffers[imageIndex]);
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
            image = _images[imageIndex],
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
        _vkd.vkCmdPipelineBarrier2(_commandBuffers[imageIndex], &barrierPresentDependencyInfo);
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
