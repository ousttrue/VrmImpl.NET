using Vortice.Vulkan;
using static Vortice.Vulkan.Vulkan;

namespace VrmImpl;

public class VulkanDeviceObject : IDisposable
{
    private readonly VkDevice device;
    public readonly VkDeviceApi Api;

    public unsafe VulkanDeviceObject(
        VkInstanceApi vki,
        VkPhysicalDevice physicalDevice,
        uint graphicsFamily,
        uint presentFamily,
        ReadOnlySpan<string> deviceExtensions
    )
    {
        var uniqueQueueFamilies = new HashSet<uint>() { graphicsFamily, presentFamily };
        var queueCreateInfos = stackalloc VkDeviceQueueCreateInfo[2];
        float queuePriority = 1.0f;

        uint uniq = 0;
        foreach (var queueFamily in uniqueQueueFamilies)
        {
            queueCreateInfos[uniq].sType = VK_STRUCTURE_TYPE_DEVICE_QUEUE_CREATE_INFO;
            queueCreateInfos[uniq].queueFamilyIndex = queueFamily;
            queueCreateInfos[uniq].queueCount = 1;
            queueCreateInfos[uniq].pQueuePriorities = &queuePriority;
            ++uniq;
        }

        var enabledVk12Features = new VkPhysicalDeviceVulkan12Features
        {
            sType = VK_STRUCTURE_TYPE_PHYSICAL_DEVICE_VULKAN_1_2_FEATURES,
            descriptorIndexing = true,
            shaderSampledImageArrayNonUniformIndexing = true,
            descriptorBindingVariableDescriptorCount = true,
            runtimeDescriptorArray = true,
            bufferDeviceAddress = true,
        };
        var enabledVk13Features = new VkPhysicalDeviceVulkan13Features
        {
            sType = VK_STRUCTURE_TYPE_PHYSICAL_DEVICE_VULKAN_1_3_FEATURES,
            pNext = &enabledVk12Features,
            synchronization2 = true,
            dynamicRendering = true,
        };
        var enabledVk10Features = new VkPhysicalDeviceFeatures { samplerAnisotropy = true };

        VkPhysicalDeviceFeatures deviceFeatures = default;
        var createInfo = new VkDeviceCreateInfo
        {
            sType = VK_STRUCTURE_TYPE_DEVICE_CREATE_INFO,
            pNext = &enabledVk13Features,
            queueCreateInfoCount = uniq,
            pQueueCreateInfos = queueCreateInfos,
            pEnabledFeatures = &deviceFeatures,
        };

        ByteStringArrayAllocator extensions = [.. deviceExtensions];
        (createInfo.enabledExtensionCount, createInfo.ppEnabledExtensionNames) = extensions;

        ByteStringArrayAllocator layers = [.. VulkanInstanceObject.ValidationLayers];
        // if (InstanceObject.EnableValidationLayers)
        // {
        //     (createInfo.enabledLayerCount, createInfo.ppEnabledLayerNames) = layers;
        // }

        if (vki.vkCreateDevice(physicalDevice, &createInfo, null, out device) != VK_SUCCESS)
        {
            throw new Exception("failed to create logical device!");
        }
        Api = new VkDeviceApi(vki, device);
    }

    public unsafe void Dispose()
    {
        Api.vkDestroyDevice(null);
    }
}
