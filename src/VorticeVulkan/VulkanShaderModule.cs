using System.Runtime.InteropServices;
using Vortice.Vulkan;

namespace VrmImpl.VorticeVulkan;

public class VulkanShaderModule : IDisposable
{
    private readonly VkDeviceApi _vk;
    public readonly VkShaderModule Module;

    public unsafe VulkanShaderModule(VkDeviceApi vk, ReadOnlySpan<byte> code)
    {
        _vk = vk;

        VkShaderModuleCreateInfo createInfo = new()
        {
            sType = VkStructureType.ShaderModuleCreateInfo,
            codeSize = (nuint)code.Length,
        };
        fixed (byte* codePtr = code)
        {
            createInfo.pCode = (uint*)codePtr;
            vk.vkCreateShaderModule(in createInfo, null, out Module).ThrowIfError();
        }
    }

    public VulkanShaderModule(VkDeviceApi vk, VkDevice device, ReadOnlySpan<uint> code)
        : this(vk, MemoryMarshal.Cast<uint, byte>(code)) { }

    public unsafe void Dispose()
    {
        _vk.vkDestroyShaderModule(Module, null);
    }
}
