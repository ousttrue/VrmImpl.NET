using Vortice.Vulkan;
using static Vortice.Vulkan.Vulkan;

namespace VrmImpl;

class VulkanSwapchainObject : IDisposable
{
    static VkSurfaceFormatKHR chooseSwapSurfaceFormat(
        ReadOnlySpan<VkSurfaceFormatKHR> availableFormats
    )
    {
        foreach (var availableFormat in availableFormats)
        {
            if (
                availableFormat.format == VkFormat.B8G8R8A8Srgb
                && availableFormat.colorSpace == VkColorSpaceKHR.SrgbNonLinear
            )
            {
                return availableFormat;
            }
        }

        return availableFormats[0];
    }

    static VkPresentModeKHR chooseSwapPresentMode(
        ReadOnlySpan<VkPresentModeKHR> availablePresentModes
    )
    {
        foreach (var availablePresentMode in availablePresentModes)
        {
            if (availablePresentMode == VkPresentModeKHR.Mailbox)
            {
                return availablePresentMode;
            }
        }

        return VkPresentModeKHR.Fifo;
    }

    private readonly VkDeviceApi _vkd;
    private readonly VkSwapchainKHR _swapChain;
    public readonly VkFormat Format;
    public readonly VkExtent2D Extent;
    public readonly VkImage[] Images;
    private VkSemaphore _imageAvailableSemaphore;
    private VkSemaphore _renderFinishedSemaphore;
    private VkFence _inFlightFence;
    private readonly VkQueue _presentQueue;

    public unsafe VulkanSwapchainObject(
        VkInstanceApi vki,
        VkPhysicalDevice physicalDevice,
        VkSurfaceKHR surface,
        VkDeviceApi vkd,
        VkExtent2D windowExtent
    )
    {
        _vkd = vkd;

        var swapChainSupport = SwapchainSupportDetails.querySwapchainSupport(
            vki,
            physicalDevice,
            surface
        );

        var surfaceFormat = chooseSwapSurfaceFormat(swapChainSupport.formats);
        var presentMode = chooseSwapPresentMode(swapChainSupport.presentModes);

        var extent = swapChainSupport.CalcExtent(windowExtent);

        var imageCount = swapChainSupport.capabilities.minImageCount + 1;
        if (
            swapChainSupport.capabilities.maxImageCount > 0
            && imageCount > swapChainSupport.capabilities.maxImageCount
        )
        {
            imageCount = swapChainSupport.capabilities.maxImageCount;
        }
        var indices = VulkanQueueFamilyIndices.findQueueFamilies(vki, physicalDevice, surface);

        vkd.vkGetDeviceQueue(indices.PresentFamily, 0, out _presentQueue);

        {
            var createInfo = new VkSwapchainCreateInfoKHR
            {
                sType = VK_STRUCTURE_TYPE_SWAPCHAIN_CREATE_INFO_KHR,
                surface = surface,
                minImageCount = imageCount,
                imageFormat = surfaceFormat.format,
                imageColorSpace = surfaceFormat.colorSpace,
                imageExtent = extent,
                imageArrayLayers = 1,
                imageUsage = VkImageUsageFlags.ColorAttachment,
                preTransform = swapChainSupport.capabilities.currentTransform,
                // compositeAlpha = CompositeAlphaFlagsKHR.CompositeAlphaOpaqueBitKhr;
                compositeAlpha = VkCompositeAlphaFlagsKHR.Opaque,
                presentMode = presentMode,
                clipped = true,
                oldSwapchain = default,
            };

            var queueFamilyIndices = stackalloc uint[]
            {
                indices.GraphicsFamily,
                indices.PresentFamily,
            };
            if (indices.GraphicsFamily != indices.PresentFamily)
            {
                createInfo.imageSharingMode = VkSharingMode.Concurrent;
                createInfo.queueFamilyIndexCount = 2;
                createInfo.pQueueFamilyIndices = queueFamilyIndices;
            }
            else
            {
                createInfo.imageSharingMode = VkSharingMode.Exclusive;
            }

            if (vkd.vkCreateSwapchainKHR(&createInfo, null, out _swapChain) != VK_SUCCESS)
            {
                throw new Exception("failed to create swap chain!");
            }
        }

        Format = surfaceFormat.format;
        Extent = extent;

        vkd.vkGetSwapchainImagesKHR(_swapChain, out imageCount);
        Span<VkImage> swapchainImages = stackalloc VkImage[(int)imageCount];
        vkd.vkGetSwapchainImagesKHR(_swapChain, swapchainImages);
        Images = swapchainImages.ToArray();

        var semaphoreInfo = new VkSemaphoreCreateInfo
        {
            sType = VK_STRUCTURE_TYPE_SEMAPHORE_CREATE_INFO,
        };
        var fenceInfo = new VkFenceCreateInfo
        {
            sType = VK_STRUCTURE_TYPE_FENCE_CREATE_INFO,
            flags = VkFenceCreateFlags.Signaled,
        };
        if (
            vkd.vkCreateSemaphore(&semaphoreInfo, null, out _imageAvailableSemaphore) != VK_SUCCESS
            || vkd.vkCreateSemaphore(&semaphoreInfo, null, out _renderFinishedSemaphore)
                != VK_SUCCESS
            || vkd.vkCreateFence(&fenceInfo, null, out _inFlightFence) != VK_SUCCESS
        )
        {
            throw new Exception("failed to create synchronization objects for a frame!");
        }
    }

    public unsafe void Dispose()
    {
        _vkd.vkDestroySemaphore(_renderFinishedSemaphore, null);
        _vkd.vkDestroySemaphore(_imageAvailableSemaphore, null);
        _vkd.vkDestroyFence(_inFlightFence, null);
        _vkd.vkDestroySwapchainKHR(_swapChain, null);
    }

    public unsafe (uint, VkSemaphore, VkSemaphore, VkFence) Acquire()
    {
        _vkd.vkDeviceWaitIdle();

        var _inFlightFence = this._inFlightFence;
        _vkd.vkWaitForFences(1, &_inFlightFence, true, ulong.MaxValue);
        _vkd.vkResetFences(1, &_inFlightFence);

        _vkd.vkAcquireNextImageKHR(
            _swapChain,
            ulong.MaxValue,
            _imageAvailableSemaphore,
            default,
            out var imageIndex
        );

        return (imageIndex, _imageAvailableSemaphore, _renderFinishedSemaphore, _inFlightFence);
    }

    public unsafe void Present(uint imageIndex, VkSemaphore renderFinishedSemaphore)
    {
        var swapChains = stackalloc VkSwapchainKHR[] { _swapChain };
        var signalSemaphores = stackalloc VkSemaphore[] { renderFinishedSemaphore };
        var presentInfo = new VkPresentInfoKHR
        {
            sType = VK_STRUCTURE_TYPE_PRESENT_INFO_KHR,
            waitSemaphoreCount = 1,
            pWaitSemaphores = signalSemaphores,
            swapchainCount = 1,
            pSwapchains = swapChains,
            pImageIndices = &imageIndex,
        };

        _vkd.vkQueuePresentKHR(_presentQueue, &presentInfo);
    }
}
