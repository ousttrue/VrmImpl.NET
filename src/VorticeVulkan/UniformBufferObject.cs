using System.Runtime.InteropServices;
using Vortice.Vulkan;

namespace VrmImpl.VorticeVulkan;

public class UniformBufferObject<T> : IDisposable
    where T : unmanaged
{
    private readonly VkDeviceApi _vkd;
    public readonly VkBuffer Buffer;
    private readonly VkDeviceMemory _memory;

    public UniformBufferObject(VkInstanceApi vki, VkDeviceApi vkd, VkPhysicalDevice physicalDevice)
    {
        _vkd = vkd;

        VkHelper.CreateBuffer(
            vki,
            vkd,
            physicalDevice,
            (uint)Marshal.SizeOf<T>(),
            VkBufferUsageFlags.UniformBuffer,
            VkMemoryPropertyFlags.HostVisible | VkMemoryPropertyFlags.HostCoherent,
            out Buffer,
            out _memory
        );
    }

    public unsafe void Dispose()
    {
        _vkd.vkDestroyBuffer(Buffer, null);
        _vkd.vkFreeMemory(_memory, null);
    }

    public unsafe void Upload(T data)
    {
        void* p;
        _vkd.vkMapMemory(_memory, 0, (ulong)Marshal.SizeOf<T>(), 0, &p);
        new Span<T>(p, 1)[0] = data;
        _vkd.vkUnmapMemory(_memory);
    }
}
