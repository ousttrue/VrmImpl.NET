using Vortice.Vulkan;
using static Vortice.Vulkan.Vulkan;

namespace VrmImpl;

public class OneTimeCommandBuffer : IDisposable
{
    private readonly VkDeviceApi _vd;
    private readonly VkQueue _graphicsQueue;
    private readonly VkCommandPool _pool;

    public OneTimeCommandBuffer(VkDeviceApi vd, uint graphicsQueueFamilyIndex)
    {
        _vd = vd;
        _vd.vkGetDeviceQueue(graphicsQueueFamilyIndex, 0, out _graphicsQueue);
        _pool = CreateCommandPool(_vd, graphicsQueueFamilyIndex);
    }

    public static unsafe VkCommandPool CreateCommandPool(
        VkDeviceApi vd,
        uint graphicsQueueFamilyIndex
    )
    {
        VkCommandPoolCreateInfo poolInfo = new()
        {
            sType = VK_STRUCTURE_TYPE_COMMAND_POOL_CREATE_INFO,
            queueFamilyIndex = graphicsQueueFamilyIndex,
            flags = VkCommandPoolCreateFlags.ResetCommandBuffer,
        };
        if (vd.vkCreateCommandPool(in poolInfo, null, out var commandPool) != VK_SUCCESS)
        {
            throw new Exception("failed to create command pool!");
        }
        return commandPool;
    }

    public unsafe void Dispose()
    {
        _vd.vkDestroyCommandPool(_pool, null);
    }

    private unsafe VkCommandBuffer Begin()
    {
        VkCommandBufferAllocateInfo allocateInfo = new()
        {
            sType = VK_STRUCTURE_TYPE_COMMAND_BUFFER_ALLOCATE_INFO,
            level = VkCommandBufferLevel.Primary,
            commandPool = _pool,
            commandBufferCount = 1,
        };

        VkCommandBuffer commandBuffer;
        _vd.vkAllocateCommandBuffers(&allocateInfo, &commandBuffer);

        VkCommandBufferBeginInfo beginInfo = new()
        {
            sType = VK_STRUCTURE_TYPE_COMMAND_BUFFER_BEGIN_INFO,
            flags = VkCommandBufferUsageFlags.OneTimeSubmit,
        };

        _vd.vkBeginCommandBuffer(commandBuffer, &beginInfo);

        return commandBuffer;
    }

    private unsafe void End(VkCommandBuffer commandBuffer)
    {
        _vd.vkEndCommandBuffer(commandBuffer);

        VkSubmitInfo submitInfo = new()
        {
            sType = VK_STRUCTURE_TYPE_SUBMIT_INFO,
            commandBufferCount = 1,
            pCommandBuffers = &commandBuffer,
        };

        _vd.vkQueueSubmit(_graphicsQueue, 1, &submitInfo, default);
        _vd.vkQueueWaitIdle(_graphicsQueue);

        _vd.vkFreeCommandBuffers(_pool, 1, &commandBuffer);
    }

    public void Execute(Action<VkCommandBuffer> callback)
    {
        var commandBuffer = Begin();
        callback(commandBuffer);
        End(commandBuffer);
    }
}
