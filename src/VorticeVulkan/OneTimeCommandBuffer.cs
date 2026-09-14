using Vortice.Vulkan;

namespace VrmImpl.VorticeVulkan;

public class OneTimeCommandBuffer : IDisposable
{
    private readonly VkDeviceApi _vk;
    private readonly VkQueue _graphicsQueue;
    private readonly VkCommandPool _pool;

    public OneTimeCommandBuffer(VkDeviceApi vk, uint graphicsQueueFamilyIndex)
    {
        _vk = vk;
        _vk.vkGetDeviceQueue(graphicsQueueFamilyIndex, 0, out _graphicsQueue);
        _pool = VkHelper.CreateCommandPool(vk, graphicsQueueFamilyIndex);
    }

    public unsafe void Dispose()
    {
        _vk.vkDestroyCommandPool(_pool, null);
    }

    private unsafe VkCommandBuffer Begin()
    {
        VkCommandBufferAllocateInfo allocateInfo = new()
        {
            sType = VkStructureType.CommandBufferAllocateInfo,
            level = VkCommandBufferLevel.Primary,
            commandPool = _pool,
            commandBufferCount = 1,
        };
        VkCommandBuffer commandBuffer;
        _vk.vkAllocateCommandBuffers(&allocateInfo, &commandBuffer);

        VkCommandBufferBeginInfo beginInfo = new()
        {
            sType = VkStructureType.CommandBufferBeginInfo,
            flags = VkCommandBufferUsageFlags.OneTimeSubmit,
        };
        _vk.vkBeginCommandBuffer(commandBuffer, &beginInfo);

        return commandBuffer;
    }

    private unsafe void End(VkCommandBuffer commandBuffer)
    {
        _vk.vkEndCommandBuffer(commandBuffer);

        VkSubmitInfo submitInfo = new()
        {
            sType = VkStructureType.SubmitInfo,
            commandBufferCount = 1,
            pCommandBuffers = &commandBuffer,
        };

        _vk.vkQueueSubmit(_graphicsQueue, 1, &submitInfo, default);
        _vk.vkQueueWaitIdle(_graphicsQueue);

        _vk.vkFreeCommandBuffers(_pool, 1, &commandBuffer);
    }

    public void Execute(Action<VkCommandBuffer> callback)
    {
        var commandBuffer = Begin();
        callback(commandBuffer);
        End(commandBuffer);
    }
}
