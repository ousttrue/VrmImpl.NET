using Vortice.Vulkan;
using static Vortice.Vulkan.Vulkan;

namespace VrmImpl;

public class DescriptorPoolObject : IDisposable
{
    private readonly VkDeviceApi api;
    public readonly VkDescriptorSetLayout _layout;
    private readonly VkDescriptorPool _descriptorPool;
    public readonly VkDescriptorSet[] DescriptorSets;
    int _pos;
    uint _lastFrameCount = uint.MaxValue;

    public unsafe DescriptorPoolObject(
        VkDeviceApi _api,
        VkDescriptorSetLayout layout,
        ReadOnlySpan<VkDescriptorSetLayoutBinding> binds,
        // scene 内での primitive 数必要
        uint maxSets
    )
    {
        api = _api;
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
                sType = VK_STRUCTURE_TYPE_DESCRIPTOR_POOL_CREATE_INFO,
                poolSizeCount = (uint)poolSizes.Length,
                pPoolSizes = ppoolSizes,
                maxSets = maxSets,
            };
            if (api.vkCreateDescriptorPool(in poolInfo, null, out _descriptorPool) != VK_SUCCESS)
            {
                throw new Exception("failed to create descriptor pool!");
            }
        }

        AllocateDescriptorSets(api, _descriptorPool, _layout, maxSets, out DescriptorSets);
    }

    public static unsafe void AllocateDescriptorSets(
        VkDeviceApi api,
        VkDescriptorPool pool,
        VkDescriptorSetLayout layout,
        uint maxSets,
        out VkDescriptorSet[] descriptorSets
    )
    {
        descriptorSets = new VkDescriptorSet[maxSets];

        var layouts = stackalloc VkDescriptorSetLayout[(int)maxSets];
        new Span<VkDescriptorSetLayout>(layouts, (int)maxSets).Fill(layout);
        var allocateInfo = new VkDescriptorSetAllocateInfo
        {
            sType = VK_STRUCTURE_TYPE_DESCRIPTOR_SET_ALLOCATE_INFO,
            descriptorPool = pool,
            descriptorSetCount = maxSets,
            pSetLayouts = layouts,
        };
        fixed (VkDescriptorSet* descriptorSetsPtr = descriptorSets)
        {
            if (api.vkAllocateDescriptorSets(in allocateInfo, descriptorSetsPtr) != VK_SUCCESS)
            {
                throw new Exception("failed to allocate descriptor sets!");
            }
        }
    }

    public unsafe void Dispose()
    {
        api.vkDestroyDescriptorPool(_descriptorPool, null);
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
