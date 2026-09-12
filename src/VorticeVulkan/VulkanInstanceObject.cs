// https://github.com/Overv/VulkanTutorial/blob/main/code/15_hello_triangle.cpp

using System.Runtime.InteropServices;
using System.Text;
using Vortice.Vulkan;
using static Vortice.Vulkan.Vulkan;

namespace VrmImpl.VorticeVulkan;

public class VulkanInstanceObject : IDisposable
{
    public const bool EnableValidationLayers =
#if DEBUG
        true;
#else
        false;
#endif

    public static readonly string[] ValidationLayers =
    [
        Encoding.ASCII.GetString(VK_LAYER_KHRONOS_VALIDATION_EXTENSION_NAME),
    ];

    private static unsafe bool checkValidationLayerSupport()
    {
        vkEnumerateInstanceLayerProperties(out var layerCount);
        Span<VkLayerProperties> availableLayers = stackalloc VkLayerProperties[(int)layerCount];
        vkEnumerateInstanceLayerProperties(availableLayers);

        foreach (var layerName in ValidationLayers)
        {
            bool layerFound = false;

            foreach (var available in availableLayers)
            {
                var availableLayerName = Marshal.PtrToStringAnsi((nint)available.layerName);
                if (layerName == availableLayerName)
                {
                    layerFound = true;
                    break;
                }
            }

            if (!layerFound)
            {
                return false;
            }
        }

        return true;
    }

    private static unsafe bool checkDeviceExtensionSupport(
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

    private static bool isDeviceSwapchainSupport(
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

    public readonly VkInstance Instance;
    public VkInstanceApi Api;
    private VulkanDebugUtilsMessengerObject debugUtilsMessenger;

    public unsafe VulkanInstanceObject(ReadOnlySpan<IntPtr> instanceExtensions)
    {
        vkInitialize();

        if (EnableValidationLayers && !checkValidationLayerSupport())
        {
            throw new Exception("validation layers requested, but not available!");
        }

        using var extensions = new ByteStringArrayAllocator();
        extensions.AddSpan(instanceExtensions);
        if (EnableValidationLayers)
        {
            extensions.Add(VK_EXT_DEBUG_UTILS_EXTENSION_NAME);
        }

        var appInfo = new VkApplicationInfo
        {
            sType = VK_STRUCTURE_TYPE_APPLICATION_INFO,
            pApplicationName = new VkUtf8ReadOnlyString("Hello Triangle"u8),
            applicationVersion = new VkVersion(1, 0, 0),
            pEngineName = new VkUtf8ReadOnlyString("No Engine"u8),
            engineVersion = new VkVersion(1, 0, 0),
            apiVersion = VK_API_VERSION_1_3,
        };
        var createInfo = new VkInstanceCreateInfo
        {
            sType = VK_STRUCTURE_TYPE_INSTANCE_CREATE_INFO,
            pApplicationInfo = &appInfo,
            enabledLayerCount = 0,
            pNext = null,
        };
        (createInfo.enabledExtensionCount, createInfo.ppEnabledExtensionNames) = extensions;

        VkDebugUtilsMessengerCreateInfoEXT debugCreateInfo = VulkanDebugUtilsMessengerObject.CreateInfo;
        ByteStringArrayAllocator layers = [.. ValidationLayers];
        if (EnableValidationLayers)
        {
            (createInfo.enabledLayerCount, createInfo.ppEnabledLayerNames) = layers;
            createInfo.pNext = &debugCreateInfo;
        }
        if (vkCreateInstance(&createInfo, null, out Instance) != VK_SUCCESS)
        {
            throw new Exception("failed to create instance!");
        }
        Api = new VkInstanceApi(Instance);

        if (EnableValidationLayers)
        {
            debugUtilsMessenger = new(Api);
        }
    }

    public unsafe void Dispose()
    {
        if (EnableValidationLayers)
        {
            debugUtilsMessenger.Dispose();
        }

        Api.vkDestroyInstance(null);

        vkShutdown();
    }

    public unsafe VkPhysicalDevice pickPhysicalDevice(ReadOnlySpan<string> deviceExtensions, VkSurfaceKHR surface)
    {
        uint physicalDeviceCount = 0;
        Api.vkEnumeratePhysicalDevices(&physicalDeviceCount, null);

        if (physicalDeviceCount == 0)
        {
            throw new Exception("failed to find GPUs with Vulkan support!");
        }

        Span<VkPhysicalDevice> physicalDevices =
            stackalloc VkPhysicalDevice[(int)physicalDeviceCount];
        Api.vkEnumeratePhysicalDevices(physicalDevices);
        foreach (var physicalDevice in physicalDevices)
        {
            if (
                checkDeviceExtensionSupport(Api, physicalDevice, deviceExtensions)
                && isDeviceSwapchainSupport(Api, physicalDevice, surface)
            )
            {
                if (physicalDevice.Handle == default)
                {
                    throw new Exception("failed to find a suitable GPU!");
                }
                return physicalDevice;
            }
        }

        throw new Exception("failed to find a suitable GPU!");
    }
}
