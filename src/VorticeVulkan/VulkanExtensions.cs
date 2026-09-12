using Vortice.Vulkan;
using static Vortice.Vulkan.Vulkan;

namespace VrmImpl.VorticeVulkan;

public static class VkExtensions
{
    public static void ThrowIfError(this VkResult err)
    {
        if (err == VK_SUCCESS)
            return;
        if (err < 0)
            throw new Exception($"[vulkan] Error: VkResult = {err}");
        Console.Error.WriteLine("[vulkan] Error: VkResult = {err}");
    }
}
