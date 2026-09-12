// https://github.com/Overv/VulkanTutorial/blob/main/code/15_hello_triangle.cpp

using Vortice.Vulkan;

namespace VrmImpl.VorticeVulkan;

public record struct VulkanSwapchainSupportDetails(
    VkSurfaceCapabilitiesKHR capabilities,
    VkSurfaceFormatKHR[] formats,
    VkPresentModeKHR[] presentModes
)
{
    public static VulkanSwapchainSupportDetails querySwapchainSupport(
        VkInstanceApi vki,
        VkPhysicalDevice physicalDevice,
        VkSurfaceKHR surface
    )
    {
        vki.vkGetPhysicalDeviceSurfaceCapabilitiesKHR(
            physicalDevice,
            surface,
            out var capabilities
        );

        vki.vkGetPhysicalDeviceSurfaceFormatsKHR(physicalDevice, surface, out var formatCount);
        if (formatCount == 0)
        {
            throw new Exception("No surface format");
        }
        Span<VkSurfaceFormatKHR> formats = stackalloc VkSurfaceFormatKHR[(int)formatCount];
        vki.vkGetPhysicalDeviceSurfaceFormatsKHR(physicalDevice, surface, formats);

        vki.vkGetPhysicalDeviceSurfacePresentModesKHR(
            physicalDevice,
            surface,
            out var presentModeCount
        );
        if (presentModeCount == 0)
        {
            throw new Exception("No present mode");
        }
        Span<VkPresentModeKHR> presentModes = stackalloc VkPresentModeKHR[(int)presentModeCount];
        vki.vkGetPhysicalDeviceSurfacePresentModesKHR(physicalDevice, surface, presentModes);

        return new(capabilities, formats.ToArray(), presentModes.ToArray());
    }

    public VkExtent2D CalcExtent(VkExtent2D actualExtent)
    {
        if (capabilities.currentExtent.width != uint.MaxValue)
        {
            return capabilities.currentExtent;
        }

        actualExtent.width = Math.Clamp(
            actualExtent.width,
            capabilities.minImageExtent.width,
            capabilities.maxImageExtent.width
        );
        actualExtent.height = Math.Clamp(
            actualExtent.height,
            capabilities.minImageExtent.height,
            capabilities.maxImageExtent.height
        );

        return actualExtent;
    }

    public VkSurfaceFormatKHR ChooseSwapSurfaceFormat(
        ReadOnlySpan<VkFormat> request_formats,
        VkColorSpaceKHR request_color_space
    )
    {
        // First check if only one format, VK_FORMAT_UNDEFINED, is available, which would imply that any format is available
        if (formats.Length == 1)
        {
            if (formats[0].format == VkFormat.Undefined)
            {
                return new VkSurfaceFormatKHR
                {
                    format = request_formats[0],
                    colorSpace = request_color_space,
                };
            }
            else
            {
                // No point in searching another format
                return formats[0];
            }
        }
        else
        {
            // Request several formats, the first found will be used
            for (int request_i = 0; request_i < request_formats.Length; request_i++)
                foreach (var avail in formats)
                    if (
                        avail.format == request_formats[request_i]
                        && avail.colorSpace == request_color_space
                    )
                        return avail;

            // If none of the requested image formats could be found, use the first available
            return formats[0];
        }
    }

    public VkSurfaceFormatKHR ChooseSwapSurfaceFormat()
    {
        // Select Surface Format
        ReadOnlySpan<VkFormat> requestSurfaceImageFormat =
        [
            VkFormat.B8G8R8A8Unorm,
            VkFormat.R8G8B8A8Unorm,
            VkFormat.B8G8R8Unorm,
            VkFormat.R8G8B8Unorm,
        ];
        var requestSurfaceColorSpace = VkColorSpaceKHR.SrgbNonLinear;
        return ChooseSwapSurfaceFormat(requestSurfaceImageFormat, requestSurfaceColorSpace);
    }

    public VkPresentModeKHR ChooseSwapPresentMode(ReadOnlySpan<VkPresentModeKHR> request_modes)
    {
        foreach (var request in request_modes)
        {
            foreach (var avail in presentModes)
                if (request == avail)
                    return request;
        }

        return VkPresentModeKHR.Fifo;
    }

    public VkPresentModeKHR ChooseSwapPresentMode()
    {
#if APP_USE_UNLIMITED_FRAME_RATE
        ReadOnlySpan<PresentModeKHR> present_modes =
        [
            PresentModeKHR.MailboxKhr,
            PresentModeKHR.ImmediateKhr,
            PresentModeKHR.FifoKhr,
        ];
#else
        ReadOnlySpan<VkPresentModeKHR> present_modes = [VkPresentModeKHR.Fifo];
#endif
        return ChooseSwapPresentMode(present_modes);
    }

    //     uint ImGui_ImplVulkanH_GetMinImageCountFromPresentMode(VkPresentModeKHR present_mode)
    //     {
    //         if (present_mode == VkPresentModeKHR.Mailbox)
    //             return 3;
    //         if (present_mode == VkPresentModeKHR.Fifo || present_mode == VkPresentModeKHR.FifoRelaxed)
    //             return 2;
    //         if (present_mode == VkPresentModeKHR.Immediate)
    //             return 1;

    //         throw new Exception();
    //         // IM_ASSERT(0);
    //         // return 1;
    //     }

    //         // Check for WSI support
    //         // _vi.vkGetPhysicalDeviceSurfaceSupportKHR(PhysicalDevice, QueueFamily, Surface, out var res)
    //         //     .CheckResult();
}
