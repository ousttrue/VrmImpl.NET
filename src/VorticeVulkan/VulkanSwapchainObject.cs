// https://github.com/Overv/VulkanTutorial/blob/main/code/15_hello_triangle.cpp

using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Vortice.Vulkan;
using static Vortice.Vulkan.Vulkan;

namespace VrmImpl.VorticeVulkan;

public class VulkanSwapchainObject : IDisposable
{
    private readonly VkInstanceApi _vki;
    private readonly VkPhysicalDevice _physicalDevice;
    private readonly VkSurfaceKHR _surface;
    private readonly VkDeviceApi _vkd;

    private VkSurfaceFormatKHR _surfaceFormat;
    public VkFormat Format => _surfaceFormat.format;
    private VkSwapchainKHR _swapChain;
    public VkSwapchainKHR Handle => _swapChain;
    public VkExtent2D Extent;
    public VkImage[] Images;
    private VkSemaphore _imageAvailableSemaphore;
    private VkSemaphore _renderFinishedSemaphore;
    private VkFence _inFlightFence;
    private VkQueue _presentQueue;

    public unsafe VulkanSwapchainObject(
        VkInstanceApi vki,
        VkPhysicalDevice physicalDevice,
        VkSurfaceKHR surface,
        VkDeviceApi vkd,
        VkExtent2D extent
    )
    {
        _vki = vki;
        _physicalDevice = physicalDevice;
        _surface = surface;
        _vkd = vkd;

        Create(extent, default);

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
            _vkd.vkCreateSemaphore(&semaphoreInfo, null, out _imageAvailableSemaphore) != VK_SUCCESS
            || _vkd.vkCreateSemaphore(&semaphoreInfo, null, out _renderFinishedSemaphore)
                != VK_SUCCESS
            || _vkd.vkCreateFence(&fenceInfo, null, out _inFlightFence) != VK_SUCCESS
        )
        {
            throw new Exception("failed to create synchronization objects for a frame!");
        }
    }

    public bool ShouldRecreate(VkExtent2D extent)
    {
        if (
            extent.width > 0
            && extent.height > 0
            && (Extent.width != extent.width || Extent.height != extent.height)
        )
        {
            return true;
        }
        return false;
    }

    public void Resize(VkExtent2D extent)
    {
        var old = _swapChain;
        Create(extent, old.Handle);
        _vkd.vkDestroySwapchainKHR(old);
    }

    private unsafe void Create(VkExtent2D windowExtent, VkSwapchainKHR? _old)
    {
        var oldExtent = Extent;
        var swapChainSupport = VulkanSwapchainSupportDetails.querySwapchainSupport(
            _vki,
            _physicalDevice,
            _surface
        );

        _surfaceFormat = swapChainSupport.ChooseSwapSurfaceFormat();
        var presentMode = swapChainSupport.ChooseSwapPresentMode();

        Extent = swapChainSupport.CalcExtent(windowExtent);

        var imageCount = swapChainSupport.capabilities.minImageCount + 1;
        if (
            swapChainSupport.capabilities.maxImageCount > 0
            && imageCount > swapChainSupport.capabilities.maxImageCount
        )
        {
            imageCount = swapChainSupport.capabilities.maxImageCount;
        }
        var indices = VulkanQueueFamilyIndices.findQueueFamilies(_vki, _physicalDevice, _surface);

        _vkd.vkGetDeviceQueue(indices.PresentFamily, 0, out _presentQueue);

        {
            var createInfo = new VkSwapchainCreateInfoKHR
            {
                sType = VK_STRUCTURE_TYPE_SWAPCHAIN_CREATE_INFO_KHR,
                surface = _surface,
                minImageCount = imageCount,
                imageFormat = _surfaceFormat.format,
                imageColorSpace = _surfaceFormat.colorSpace,
                imageExtent = Extent,
                imageArrayLayers = 1,
                imageUsage = VkImageUsageFlags.ColorAttachment,
                preTransform = swapChainSupport.capabilities.currentTransform,
                // compositeAlpha = CompositeAlphaFlagsKHR.CompositeAlphaOpaqueBitKhr;
                compositeAlpha = VkCompositeAlphaFlagsKHR.Opaque,
                presentMode = presentMode,
                clipped = true,
            };
            if (_old is VkSwapchainKHR old)
            {
                createInfo.oldSwapchain = old;
            }

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

            if (_vkd.vkCreateSwapchainKHR(&createInfo, null, out _swapChain) != VK_SUCCESS)
            {
                throw new Exception("failed to create swap chain!");
            }
        }

        _vkd.vkGetSwapchainImagesKHR(_swapChain, out imageCount);
        Span<VkImage> swapchainImages = stackalloc VkImage[(int)imageCount];
        _vkd.vkGetSwapchainImagesKHR(_swapChain, swapchainImages);
        Images = swapchainImages.ToArray();

        if (_old is null)
        {
            VulkanLogger.Debug($"Swapchain: new {Extent} {Format} {presentMode} X{Images.Length}");
        }
        else
        {
            VulkanLogger.Debug($"Swapchain: {oldExtent} => {Extent}");
        }
    }

    public unsafe void Dispose()
    {
        _vkd.vkDestroySemaphore(_renderFinishedSemaphore, null);
        _vkd.vkDestroySemaphore(_imageAvailableSemaphore, null);
        _vkd.vkDestroyFence(_inFlightFence, null);
        _vkd.vkDestroySwapchainKHR(_swapChain, null);
    }

    public unsafe (
        uint ImageIndex,
        VkSemaphore ImageAvailableSemaphore,
        VkSemaphore RenderFinishedSemaphore,
        VkFence InFlightFence
    )? Acquire()
    {
        _vkd.vkDeviceWaitIdle();

        var _inFlightFence = this._inFlightFence;
        _vkd.vkWaitForFences(1, &_inFlightFence, true, ulong.MaxValue);
        _vkd.vkResetFences(1, &_inFlightFence);

        var result = _vkd.vkAcquireNextImageKHR(
            _swapChain,
            ulong.MaxValue,
            _imageAvailableSemaphore,
            default,
            out var imageIndex
        );
        if (result == VK_SUCCESS)
        {
            return (imageIndex, _imageAvailableSemaphore, _renderFinishedSemaphore, _inFlightFence);
        }
        else if (result == VK_ERROR_OUT_OF_DATE_KHR)
        {
            // recreate swapchain
            return default;
        }
        else
        {
            throw new Exception($"vkAcquireNextImageKHR: VkResult = {result}");
        }
    }

    public unsafe bool Present(uint imageIndex, VkSemaphore renderFinishedSemaphore)
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

        var result = _vkd.vkQueuePresentKHR(_presentQueue, &presentInfo);
        if (result == VK_SUCCESS || result == VK_SUBOPTIMAL_KHR)
        {
            return true;
        }
        else if (result == VK_ERROR_OUT_OF_DATE_KHR)
        {
            // recreate swapchain
            return false;
        }
        else
        {
            throw new Exception($"vkQueuePresentKHR: VkResult = {result}");
        }
    }
}
