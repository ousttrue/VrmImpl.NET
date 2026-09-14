using Vortice.Vulkan;

namespace VrmImpl.VorticeVulkan;

public class RenderTargetObject : IDisposable
{
    private readonly VkDeviceApi _vk;
    private readonly VkQueue _graphicsQueue;

    public readonly VkExtent2D Extent;
    public readonly VkFormat ColorFormat;
    public readonly VkImage[] Images;
    public readonly VkImageView[] ImageViews;

    // for depth
    public readonly VkFormat DepthFormat;

    private readonly VkImage _depthImage;
    private readonly VkDeviceMemory _depthImageMemory;
    public readonly VkImageView DepthImageView;

    // flight
    private readonly VkSemaphore[] _renderFinishedSemaphores;
    private readonly VkCommandPool _commandPool;
    private readonly VkCommandBuffer[] _commandBuffers;

    public unsafe RenderTargetObject(
        VkInstanceApi vki,
        VkDeviceApi vk,
        VkPhysicalDevice physicalDevice,
        uint graphicsQueueFamilyIndex,
        VkExtent2D extent,
        VkFormat colorFormat,
        VkImage[] images,
        VkFormat depthFormat
    )
    {
        _vk = vk;
        _vk.vkGetDeviceQueue(graphicsQueueFamilyIndex, 0, out _graphicsQueue);
        Extent = extent;
        ColorFormat = colorFormat;
        Images = images;
        DepthFormat = depthFormat;

        ImageViews = new VkImageView[images.Length];
        for (int i = 0; i < images.Length; i++)
        {
            ImageViews[i] = VkHelper.CreateImageView(
                vk,
                images[i],
                ColorFormat,
                VkImageAspectFlags.Color
            );
        }

        VkHelper.CreateImage(
            vki,
            vk,
            physicalDevice,
            extent.width,
            extent.height,
            depthFormat,
            VkImageTiling.Optimal,
            VkImageUsageFlags.DepthStencilAttachment,
            VkMemoryPropertyFlags.DeviceLocal,
            out _depthImage,
            out _depthImageMemory
        );
        DepthImageView = VkHelper.CreateImageView(
            vk,
            _depthImage,
            depthFormat,
            VkImageAspectFlags.Depth
        );

        var MAX_FRAMES_IN_FLIGHT = images.Length;
        _renderFinishedSemaphores = new VkSemaphore[MAX_FRAMES_IN_FLIGHT];

        VkSemaphoreCreateInfo semaphoreInfo = new() { sType = VkStructureType.SemaphoreCreateInfo };
        for (var i = 0; i < MAX_FRAMES_IN_FLIGHT; i++)
        {
            vk.vkCreateSemaphore(in semaphoreInfo, null, out _renderFinishedSemaphores[i])
                .ThrowIfError();
        }

        _commandPool = VkHelper.CreateCommandPool(_vk, graphicsQueueFamilyIndex);
        _commandBuffers = new VkCommandBuffer[MAX_FRAMES_IN_FLIGHT];
        VkCommandBufferAllocateInfo allocInfo = new()
        {
            sType = VkStructureType.CommandBufferAllocateInfo,
            commandPool = _commandPool,
            level = VkCommandBufferLevel.Primary,
            commandBufferCount = (uint)_commandBuffers.Length,
        };
        fixed (VkCommandBuffer* commandBuffersPtr = _commandBuffers)
        {
            vk.vkAllocateCommandBuffers(&allocInfo, commandBuffersPtr).ThrowIfError();
        }
    }

    public unsafe void Dispose()
    {
        _vk.vkDestroyCommandPool(_commandPool, null);
        for (int i = 0; i < _renderFinishedSemaphores!.Length; i++)
        {
            _vk.vkDestroySemaphore(_renderFinishedSemaphores![i], null);
        }

        foreach (var imageView in ImageViews!)
        {
            _vk.vkDestroyImageView(imageView, null);
        }

        _vk.vkDestroyImageView(DepthImageView, null);
        _vk.vkDestroyImage(_depthImage, null);
        _vk.vkFreeMemory(_depthImageMemory, null);
    }

    public unsafe (VkCommandBuffer, VkImageView, VkImage, VkSemaphore) BeginCommand(uint imageIndex)
    {
        var commandBuffer = _commandBuffers[imageIndex];
        VkCommandBufferBeginInfo beginInfo = new()
        {
            sType = VkStructureType.CommandBufferBeginInfo,
        };
        _vk.vkBeginCommandBuffer(_commandBuffers[imageIndex], &beginInfo).CheckResult();
        return (
            commandBuffer,
            ImageViews[imageIndex],
            Images[imageIndex],
            _renderFinishedSemaphores[imageIndex]
        );
    }

    public unsafe void BeginRendering(
        VkCommandBuffer commandBuffer,
        VkImageView imageView,
        VkImage image,
        //
        VkAttachmentLoadOp colorLoadOp,
        VkClearColorValue clearColor,
        VkAttachmentStoreOp colorStoreOp,
        VkAttachmentLoadOp depthLoadOp,
        VkClearDepthStencilValue clearDepthStencil,
        VkAttachmentStoreOp depthStoreOp,
        VkImageLayout? _layout
    )
    {
        var color_attachment_info = new VkRenderingAttachmentInfo
        {
            sType = VkStructureType.RenderingAttachmentInfo,
            imageView = imageView,
            imageLayout = VkImageLayout.ColorAttachmentOptimal,
            loadOp = colorLoadOp,
            storeOp = colorStoreOp,
            clearValue = new VkClearValue { color = clearColor },
        };
        var depth_attachment_info = new VkRenderingAttachmentInfo()
        {
            sType = VkStructureType.RenderingAttachmentInfo,
            imageView = DepthImageView,
            imageLayout = VkImageLayout.DepthAttachmentOptimal,
            loadOp = depthLoadOp,
            storeOp = depthStoreOp,
            clearValue = new VkClearValue { depthStencil = clearDepthStencil },
        };
        var render_info = new VkRenderingInfo
        {
            sType = VkStructureType.RenderingInfo,
            renderArea = new() { extent = Extent },
            layerCount = 1,
            colorAttachmentCount = 1,
            pColorAttachments = &color_attachment_info,
            pDepthAttachment = &depth_attachment_info,
            // PStencilAttachment = &depth_attachment_info,
        };

        if (_layout is VkImageLayout layout)
        {
            VkHelper.TransitionImageLayout(_vk, commandBuffer, image, layout);
        }

        _vk.vkCmdBeginRendering(commandBuffer, &render_info);
    }

    public unsafe void EndRenderingAndCommandBuffer(
        VkCommandBuffer commandBuffer,
        ReadOnlySpan<VkSemaphore> waitSemaphores,
        VkFence? _fence,
        //
        VkSemaphore renderFinishedSemaphore,
        VkImage image,
        VkImageLayout newLayout
    )
    {
        _vk.vkCmdEndRendering(commandBuffer);

        VkHelper.TransitionImageLayout(_vk, commandBuffer, image, newLayout);

        _vk.vkEndCommandBuffer(commandBuffer).ThrowIfError();

        // var waitSemaphores = flight.ImageAvailableSemaphore;
        Span<VkPipelineStageFlags> waitStages =
            stackalloc VkPipelineStageFlags[waitSemaphores.Length];
        waitStages.Fill(VkPipelineStageFlags.ColorAttachmentOutput);
        // var signalSemaphores = _renderFinishedSemaphores[imageIndex];
        // var fence = flight.InFlightFence;
        // var commandBuffer = _commandBuffers[imageIndex];
        fixed (VkSemaphore* pwaitSemaphores = waitSemaphores)
        fixed (VkPipelineStageFlags* pwaitStages = waitStages)
        {
            VkSubmitInfo submitInfo = new()
            {
                sType = VkStructureType.SubmitInfo,
                waitSemaphoreCount = (uint)waitSemaphores.Length,
                pWaitSemaphores = waitSemaphores.Length > 0 ? pwaitSemaphores : null,
                pWaitDstStageMask = waitSemaphores.Length > 0 ? pwaitStages : null,

                commandBufferCount = 1,
                pCommandBuffers = &commandBuffer,

                signalSemaphoreCount = 1,
                pSignalSemaphores = &renderFinishedSemaphore,
            };
            if (_fence is VkFence fence)
            {
                // swapchain
                _vk.vkResetFences(1, &fence);
                _vk.vkQueueSubmit(_graphicsQueue, 1, &submitInfo, fence).ThrowIfError();
            }
            else
            {
                _vk.vkQueueSubmit(_graphicsQueue, 1, &submitInfo, default).ThrowIfError();
            }
        }
    }
}
