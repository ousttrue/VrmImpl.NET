using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Vortice.Vulkan;
using static Vortice.Vulkan.Vulkan;

namespace VrmImpl.VorticeVulkan;

public record struct VulkanPhysicalDeviceInfo(
    VkPhysicalDevice PhysicalDevice,
    VkPhysicalDeviceProperties2 Properties,
    bool HasDeviceExtensions,
    bool IsSupportSwapchain
)
{
    const string ok = "ok";
    const string fail = "no";

    public override unsafe string ToString()
    {
        var props = Properties;
        return Marshal.PtrToStringAnsi((nint)props.properties.deviceName) ?? throw new Exception();
    }

    public static VkPhysicalDevice Pick(
        VkInstanceApi api,
        ReadOnlySpan<string> deviceExtensions,
        VkSurfaceKHR surface
    )
    {
        VkPhysicalDevice? _picked = default;

        var devices = GetPhysicalDevices(api, deviceExtensions, surface);
        for (int i = 0; i < devices.Length; ++i)
        {
            var info = devices[i];
            var has = info.HasDeviceExtensions ? ok : fail;
            var supported = info.IsSupportSwapchain ? ok : fail;
            var selected = "[ ]";
            if ((_picked is null) && info.HasDeviceExtensions && info.IsSupportSwapchain)
            {
                selected = "[*]";
                _picked = info.PhysicalDevice;
            }
            VulkanLogger.Debug(
                $"GPU#{i} {selected} Extensions: {has}, Swapchain: {supported}, {info}"
            );
        }

        if (_picked is not VkPhysicalDevice picked)
        {
            throw new Exception("no picked");
        }
        return picked;
    }

    public static unsafe VulkanPhysicalDeviceInfo[] GetPhysicalDevices(
        VkInstanceApi api,
        ReadOnlySpan<string> deviceExtensions,
        VkSurfaceKHR surface
    )
    {
        uint physicalDeviceCount = 0;
        api.vkEnumeratePhysicalDevices(&physicalDeviceCount, null);
        Span<VkPhysicalDevice> physicalDevices =
            stackalloc VkPhysicalDevice[(int)physicalDeviceCount];
        api.vkEnumeratePhysicalDevices(physicalDevices);
        var infoList = new VulkanPhysicalDeviceInfo[physicalDeviceCount];
        for (int i = 0; i < physicalDevices.Length; ++i)
        {
            var physicalDevice = physicalDevices[i];
            var hasExtensions = CheckDeviceExtensionSupport(api, physicalDevice, deviceExtensions);
            var isSupportSwapchain = IsDeviceSwapchainSupport(api, physicalDevice, surface);
            var deviceProperties = new VkPhysicalDeviceProperties2
            {
                sType = VK_STRUCTURE_TYPE_PHYSICAL_DEVICE_PROPERTIES_2,
            };
            api.vkGetPhysicalDeviceProperties2(physicalDevice, &deviceProperties);
            infoList[i] = new(physicalDevice, deviceProperties, hasExtensions, isSupportSwapchain);
        }
        return infoList;
    }

    private static unsafe bool CheckDeviceExtensionSupport(
        VkInstanceApi vki,
        VkPhysicalDevice physicalDevice,
        ReadOnlySpan<string> deviceExtensions
    )
    {
        uint extensionCount;
        vki.vkEnumerateDeviceExtensionProperties(physicalDevice, null, &extensionCount, null);

        var availableExtensions = stackalloc VkExtensionProperties[(int)extensionCount];
        vki.vkEnumerateDeviceExtensionProperties(
            physicalDevice,
            (byte*)null,
            &extensionCount,
            availableExtensions
        );

        HashSet<string> requiredExtensions = [.. deviceExtensions];
        for (int i = 0; i < extensionCount; ++i)
        {
            var extensionName =
                Marshal.PtrToStringAnsi((nint)availableExtensions[i].extensionName)
                ?? throw new Exception();
            requiredExtensions.Remove(extensionName);
        }

        return requiredExtensions.Count == 0;
    }

    private static bool IsDeviceSwapchainSupport(
        VkInstanceApi vki,
        VkPhysicalDevice physicalDevice,
        VkSurfaceKHR surface
    )
    {
        var swapchainSupport = VulkanSwapchainSupportDetails.querySwapchainSupport(
            vki,
            physicalDevice,
            surface
        );
        return swapchainSupport.formats.Length > 0 && swapchainSupport.presentModes.Length > 0;
    }
}
