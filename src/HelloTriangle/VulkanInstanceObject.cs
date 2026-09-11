using System.Runtime.InteropServices;
using System.Text;
using Vortice.Vulkan;
using static Vortice.Vulkan.Vulkan;

namespace VrmImpl;

class VulkanInstanceObject : IDisposable
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

    private VkInstance instance;
    public VkInstanceApi Api;
    private VulkanDebugUtilsMessengerObject debugUtilsMessenger;
    public VkSurfaceKHR Surface;

    public unsafe VulkanInstanceObject(GlfwWindow _window)
    {
        vkInitialize();

        if (EnableValidationLayers && !checkValidationLayerSupport())
        {
            throw new Exception("validation layers requested, but not available!");
        }

        using var instanceExtensions = new ByteStringArrayAllocator();
        instanceExtensions.AddSpan(_window.GetVkExtensions());
        if (EnableValidationLayers)
        {
            instanceExtensions.Add(VK_EXT_DEBUG_UTILS_EXTENSION_NAME);
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
        (createInfo.enabledExtensionCount, createInfo.ppEnabledExtensionNames) = instanceExtensions;

        VkDebugUtilsMessengerCreateInfoEXT debugCreateInfo =
            VulkanDebugUtilsMessengerObject.CreateInfo;
        ByteStringArrayAllocator layers = [.. ValidationLayers];
        if (EnableValidationLayers)
        {
            (createInfo.enabledLayerCount, createInfo.ppEnabledLayerNames) = layers;
            createInfo.pNext = &debugCreateInfo;
        }
        if (vkCreateInstance(&createInfo, null, out instance) != VK_SUCCESS)
        {
            throw new Exception("failed to create instance!");
        }
        Api = new VkInstanceApi(instance);

        if (EnableValidationLayers)
        {
            debugUtilsMessenger = new(Api);
        }
        Surface = new(_window.CreateVkSurface(instance.Handle));
    }

    public unsafe void Dispose()
    {
        if (EnableValidationLayers)
        {
            debugUtilsMessenger.Dispose();
        }

        Api.vkDestroySurfaceKHR(Surface, null);
        Api.vkDestroyInstance(null);

        vkShutdown();
    }
}
