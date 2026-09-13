using System.Runtime.InteropServices;
using Vortice.Vulkan;
using static Vortice.Vulkan.Vulkan;

namespace VrmImpl.Gui;

public class ShaderModuleObject : IDisposable
{
    private readonly VkDeviceApi _vd;
    private readonly VkDevice _device;
    public VkShaderModule Module;

    public unsafe ShaderModuleObject(VkDeviceApi vd, VkDevice device, ReadOnlySpan<byte> code)
    {
        _vd = vd;
        _device = device;

        VkShaderModuleCreateInfo createInfo = new()
        {
            sType = VK_STRUCTURE_TYPE_SHADER_MODULE_CREATE_INFO,
            codeSize = (nuint)code.Length,
        };
        fixed (byte* codePtr = code)
        {
            createInfo.pCode = (uint*)codePtr;
            if (vd.vkCreateShaderModule(&createInfo, null, out var module) != VK_SUCCESS)
            {
                throw new Exception();
            }
            Module = module;
        }
    }

    public ShaderModuleObject(VkDeviceApi vd, VkDevice device, ReadOnlySpan<uint> code)
        : this(vd, device, MemoryMarshal.Cast<uint, byte>(code)) { }

    public unsafe void Dispose()
    {
        _vd.vkDestroyShaderModule(Module, null);
    }
}
