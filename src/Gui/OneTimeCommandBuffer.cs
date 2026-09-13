using Vortice.Vulkan;
using VrmImpl.VorticeVulkan;

namespace VrmImpl.Gui;

public class OneTimeCommandBuffer : IDisposable
{
    private readonly VkDeviceApi _vkd;
    private readonly VkQueue _graphicsQueue;
    private readonly VkCommandPool _pool;

    public unsafe OneTimeCommandBuffer(VkDeviceApi vkd, uint graphicsQueueFamilyIndex)
    {
        _vkd = vkd;
        _vkd.vkGetDeviceQueue(graphicsQueueFamilyIndex, 0, out _graphicsQueue);

        VkCommandPoolCreateInfo poolInfo = new()
        {
            sType = VkStructureType.CommandPoolCreateInfo,
            queueFamilyIndex = graphicsQueueFamilyIndex,
            flags = VkCommandPoolCreateFlags.ResetCommandBuffer,
        };
        _vkd.vkCreateCommandPool(in poolInfo, null, out _pool).ThrowIfError();
    }

    public unsafe void Dispose()
    {
        _vkd.vkDestroyCommandPool(_pool, null);
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
        _vkd.vkAllocateCommandBuffer(&allocateInfo, out var commandBuffer);

        VkCommandBufferBeginInfo beginInfo = new()
        {
            sType = VkStructureType.CommandBufferBeginInfo,
            flags = VkCommandBufferUsageFlags.OneTimeSubmit,
        };
        _vkd.vkBeginCommandBuffer(commandBuffer, &beginInfo);

        return commandBuffer;
    }

    private unsafe void End(VkCommandBuffer commandBuffer)
    {
        _vkd.vkEndCommandBuffer(commandBuffer);

        VkSubmitInfo submitInfo = new()
        {
            sType = VkStructureType.SubmitInfo,
            commandBufferCount = 1,
            pCommandBuffers = &commandBuffer,
        };
        _vkd.vkQueueSubmit(_graphicsQueue, 1, &submitInfo, default);

        _vkd.vkQueueWaitIdle(_graphicsQueue);
        _vkd.vkFreeCommandBuffers(_pool, 1, &commandBuffer);
    }

    public void Execute(Action<VkCommandBuffer> callback)
    {
        var commandBuffer = Begin();
        callback(commandBuffer);
        End(commandBuffer);
    }
}
