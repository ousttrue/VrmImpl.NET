using Vortice.Vulkan;

namespace VrmImpl.VorticeVulkan;

public class DescriptorPoolObject : IDisposable
{
    private readonly VkDeviceApi _vk;
    public readonly VkDescriptorSetLayout _layout;
    private readonly VkDescriptorPool _descriptorPool;
    public readonly VkDescriptorSet[] DescriptorSets;
    int _pos;
    uint _lastFrameCount = uint.MaxValue;

    public unsafe DescriptorPoolObject(
        VkDeviceApi vk,
        VkDescriptorSetLayout layout,
        ReadOnlySpan<VkDescriptorSetLayoutBinding> binds,
        // scene 内での primitive 数必要
        uint maxSets
    )
    {
        _vk = vk;
        _layout = layout;

        Dictionary<VkDescriptorType, uint> counter = new();
        foreach (var bind in binds)
        {
            if (counter.TryGetValue(bind.descriptorType, out var value))
            {
                counter[bind.descriptorType] = value + maxSets;
            }
            else
            {
                counter[bind.descriptorType] = maxSets;
            }
        }
        var poolSizes = counter
            .Select(x => new VkDescriptorPoolSize { type = x.Key, descriptorCount = x.Value })
            .ToArray();

        fixed (VkDescriptorPoolSize* ppoolSizes = poolSizes)
        {
            VkDescriptorPoolCreateInfo poolInfo = new()
            {
                sType = VkStructureType.DescriptorPoolCreateInfo,
                poolSizeCount = (uint)poolSizes.Length,
                pPoolSizes = ppoolSizes,
                maxSets = maxSets,
            };
            vk.vkCreateDescriptorPool(in poolInfo, null, out _descriptorPool).ThrowIfError();
        }

        VkHelper.AllocateDescriptorSets(vk, _descriptorPool, _layout, maxSets, out DescriptorSets);
    }

    public unsafe void Dispose()
    {
        _vk.vkDestroyDescriptorPool(_descriptorPool, null);
    }

    public VkDescriptorSet Get(uint frameCount)
    {
        if (_lastFrameCount != frameCount)
        {
            _pos = 0;
        }
        _lastFrameCount = frameCount;
        return DescriptorSets[_pos++];
    }
}
