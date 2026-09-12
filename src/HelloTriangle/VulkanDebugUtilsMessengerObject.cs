using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Vortice.Vulkan;
using static Vortice.Vulkan.Vulkan;

namespace VrmImpl;

class VulkanDebugUtilsMessengerObject : IDisposable
{
    public static readonly ILogger Logger = StaticLogger.Factory.CreateLogger("vulkan");

    [UnmanagedCallersOnly]
    private static unsafe uint debugCallback(
        VkDebugUtilsMessageSeverityFlagsEXT messageSeverity,
        VkDebugUtilsMessageTypeFlagsEXT messageTypes,
        VkDebugUtilsMessengerCallbackDataEXT* pCallbackData,
        void* userData
    )
    {
        var msg = Marshal.PtrToStringAnsi((nint)pCallbackData->pMessage);
        switch (messageSeverity)
        {
            case VkDebugUtilsMessageSeverityFlagsEXT.None:
                break;
            case VkDebugUtilsMessageSeverityFlagsEXT.Verbose:
                Logger.LogTrace($"validation layer: {msg}");
                break;
            case VkDebugUtilsMessageSeverityFlagsEXT.Info:
                Logger.LogInformation($"validation layer: {msg}");
                break;
            case VkDebugUtilsMessageSeverityFlagsEXT.Warning:
                Logger.LogWarning($"validation layer: {msg}");
                break;
            case VkDebugUtilsMessageSeverityFlagsEXT.Error:
                Logger.LogError($"validation layer: {msg}");
                break;
        }
        return VK_FALSE;
    }

    public static readonly unsafe VkDebugUtilsMessengerCreateInfoEXT CreateInfo = new()
    {
        sType = VK_STRUCTURE_TYPE_DEBUG_UTILS_MESSENGER_CREATE_INFO_EXT,
        messageSeverity =
            VkDebugUtilsMessageSeverityFlagsEXT.Verbose
            | VkDebugUtilsMessageSeverityFlagsEXT.Warning
            | VkDebugUtilsMessageSeverityFlagsEXT.Error,
        messageType =
            VkDebugUtilsMessageTypeFlagsEXT.General
            | VkDebugUtilsMessageTypeFlagsEXT.Validation
            | VkDebugUtilsMessageTypeFlagsEXT.Performance,
        pfnUserCallback = &debugCallback,
    };

    private readonly VkInstanceApi _vki;
    private VkDebugUtilsMessengerEXT _debugMessenger;

    public unsafe VulkanDebugUtilsMessengerObject(VkInstanceApi vki)
    {
        _vki = vki;
        var createInfo = CreateInfo;
        if (
            vki.vkCreateDebugUtilsMessengerEXT(&createInfo, null, out _debugMessenger) != VK_SUCCESS
        )
        {
            throw new Exception("failed to set up debug messenger!");
        }
    }

    public unsafe void Dispose()
    {
        _vki.vkDestroyDebugUtilsMessengerEXT(_debugMessenger, null);
    }
}
