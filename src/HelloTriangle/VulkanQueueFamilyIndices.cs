using Vortice.Vulkan;

namespace VrmImpl;

record struct VulkanQueueFamilyIndices(uint GraphicsFamily, uint PresentFamily)
{
    public static VulkanQueueFamilyIndices findQueueFamilies(
        VkInstanceApi vki,
        VkPhysicalDevice physicalDevice,
        VkSurfaceKHR surface
    )
    {
        vki.vkGetPhysicalDeviceQueueFamilyProperties(physicalDevice, out var queueFamilyCount);
        Span<VkQueueFamilyProperties> queueFamilies =
            stackalloc VkQueueFamilyProperties[(int)queueFamilyCount];
        vki.vkGetPhysicalDeviceQueueFamilyProperties(physicalDevice, queueFamilies);

        uint i = 0;
        uint? graphicsFamily = default;
        uint? presentFamily = default;
        foreach (var queueFamily in queueFamilies)
        {
            if (queueFamily.queueFlags.HasFlag(VkQueueFlags.Graphics))
            {
                graphicsFamily = i;
            }

            vki.vkGetPhysicalDeviceSurfaceSupportKHR(
                physicalDevice,
                i,
                surface,
                out var presentSupport
            );

            if (presentSupport)
            {
                presentFamily = i;
            }

            if (graphicsFamily is not null && presentFamily is not null)
            {
                break;
            }

            i++;
        }

        if (graphicsFamily is uint g) { }
        else
        {
            throw new Exception("GraphicsQueue not found");
        }
        if (presentFamily is uint p) { }
        else
        {
            throw new Exception("PresentQueue not found");
        }
        return new(g, p);
    }
}
