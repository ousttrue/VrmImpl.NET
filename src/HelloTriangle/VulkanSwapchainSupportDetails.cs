using Vortice.Vulkan;

namespace VrmImpl;

record struct SwapchainSupportDetails(
    VkSurfaceCapabilitiesKHR capabilities,
    VkSurfaceFormatKHR[] formats,
    VkPresentModeKHR[] presentModes
)
{
    public static SwapchainSupportDetails querySwapchainSupport(
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
}
