using System.Runtime.InteropServices;
using Vortice.Vulkan;
using static Vortice.Vulkan.Vulkan;

namespace VrmImpl;

public class ArrayBufferObject(
    VkInstanceApi vi,
    VkDeviceApi vd,
    VkDevice device,
    VkBufferUsageFlags usage,
    VkMemoryPropertyFlags memoryProps,
    uint stride
) : IDisposable
{
    private readonly VkInstanceApi _vi = vi;
    private readonly VkDeviceApi _vd = vd;
    private readonly VkDevice _device = device;
    private readonly VkBufferUsageFlags _usage = usage;
    private readonly VkMemoryPropertyFlags _memoryProps = memoryProps;
    private uint _stride = stride;
    private ulong _itemCount = 0;
    public ulong ByteLength => _stride * _itemCount;
    public VkBuffer Buffer;
    private VkDeviceMemory _memory;
    private ulong _bufferMemoryAlignment = 256;

    public ArrayBufferObject(
        VkInstanceApi vi,
        VkDeviceApi vd,
        VkDevice device,
        VkBufferUsageFlags usage,
        VkMemoryPropertyFlags memoryProps,
        VkPhysicalDevice physicalDevice,
        uint stride,
        ulong itemCount
    )
        : this(vi, vd, device, usage, memoryProps, stride)
    {
        Grow(physicalDevice, itemCount);
    }

    /// <summary>
    /// use MemoryPropertyFlags.HostVisibleBit and map
    /// </summary>
    public static unsafe ArrayBufferObject Create<T>(
        VkInstanceApi vi,
        VkDeviceApi vd,
        VkDevice device,
        VkBufferUsageFlags usage,
        VkPhysicalDevice physicalDevice,
        ReadOnlySpan<T> values
    )
        where T : unmanaged
    {
        var self = new ArrayBufferObject(
            vi,
            vd,
            device,
            usage,
            VkMemoryPropertyFlags.HostVisible | VkMemoryPropertyFlags.HostCoherent,
            physicalDevice,
            (uint)Marshal.SizeOf<T>(),
            (uint)values.Length
        );
        using (var map = self.Map())
        {
            values.CopyTo(new Span<T>(map.ToPointer<T>(), values.Length));
        }
        return self;
    }

    public static unsafe ArrayBufferObject Create(
        VkInstanceApi vi,
        VkDeviceApi vd,
        VkDevice device,
        VkBufferUsageFlags usage,
        VkPhysicalDevice physicalDevice,
        uint graphicsQueueFamilyIndex,
        uint stride,
        ReadOnlySpan<byte> data
    )
    {
        var self = new ArrayBufferObject(
            vi,
            vd,
            device,
            usage,
            VkMemoryPropertyFlags.DeviceLocal,
            physicalDevice,
            stride,
            (uint)(data.Length / stride)
        );
        using var singleTimeCommand = new OneTimeCommandBuffer(vd, graphicsQueueFamilyIndex);
        using (
            var staging = Create(
                vi,
                vd,
                device,
                VkBufferUsageFlags.TransferSrc,
                physicalDevice,
                data
            )
        )
        {
            singleTimeCommand.Execute(commandBuffer =>
            {
                CopyBuffer(vd, commandBuffer, staging.Buffer, self.Buffer, staging.ByteLength);
            });
        }
        return self;
    }

    public static unsafe void CopyBuffer(
        VkDeviceApi api,
        VkCommandBuffer commandBuffer,
        VkBuffer srcBuffer,
        VkBuffer dstBuffer,
        ulong size
    )
    {
        var copyRegion = stackalloc VkBufferCopy[1] { new VkBufferCopy { size = size } };
        api.vkCmdCopyBuffer(commandBuffer, srcBuffer, dstBuffer, 1, copyRegion);
    }

    public static unsafe ArrayBufferObject Create<T>(
        VkInstanceApi vi,
        VkDeviceApi vd,
        VkDevice device,
        VkBufferUsageFlags usage,
        VkPhysicalDevice physicalDevice,
        uint graphicsQueueFamilyIndex,
        ReadOnlySpan<T> values
    )
        where T : unmanaged
    {
        return Create(
            vi,
            vd,
            device,
            usage,
            physicalDevice,
            graphicsQueueFamilyIndex,
            (uint)Marshal.SizeOf<T>(),
            MemoryMarshal.Cast<T, byte>(values)
        );
    }

    public unsafe void Dispose()
    {
        _vd.vkDestroyBuffer(Buffer, default);
        _vd.vkFreeMemory(_memory, default);
    }

    public unsafe void Grow(VkPhysicalDevice physicalDevice, ulong itemCount)
    {
        if (Buffer.Handle != default && _itemCount >= itemCount)
        {
            return;
        }

        if (Buffer.Handle != default)
        {
            _vd.vkDestroyBuffer(Buffer, default);
        }
        if (_memory.Handle != default)
        {
            _vd.vkFreeMemory(_memory, default);
        }

        // VkHelper.CreateBuffer(
        //     _vk,
        //     physicalDevice,
        //     _device,
        //     bufferSize,
        //     ,
        //     MemoryPropertyFlags.DeviceLocalBit,
        //     ref VertexBuffer,
        //     ref vertexBufferMemory
        // );

        ulong sizeAlignedVertexBuffer =
            ((itemCount * _stride - 1) / _bufferMemoryAlignment + 1) * _bufferMemoryAlignment;
        var bufferInfo = new VkBufferCreateInfo
        {
            sType = VK_STRUCTURE_TYPE_BUFFER_CREATE_INFO,
            size = sizeAlignedVertexBuffer,
            usage = _usage,
            sharingMode = VkSharingMode.Exclusive,
        };
        if (_vd.vkCreateBuffer(in bufferInfo, default, out Buffer) != VK_SUCCESS)
        {
            throw new Exception($"Unable to create a device buffer");
        }

        _vd.vkGetBufferMemoryRequirements(Buffer, out var req);
        _bufferMemoryAlignment =
            (_bufferMemoryAlignment > req.alignment) ? _bufferMemoryAlignment : req.alignment;
        var allocInfo = new VkMemoryAllocateInfo
        {
            sType = VK_STRUCTURE_TYPE_MEMORY_ALLOCATE_INFO,
            allocationSize = req.size,
            memoryTypeIndex = FindMemoryType(_vi, physicalDevice, req.memoryTypeBits, _memoryProps),
        };
        if (_vd.vkAllocateMemory(&allocInfo, default, out _memory) != VK_SUCCESS)
        {
            throw new Exception($"Unable to allocate device memory");
        }

        if (_vd.vkBindBufferMemory(Buffer, _memory, 0) != VK_SUCCESS)
        {
            throw new Exception($"Unable to bind device memory");
        }
        _itemCount = req.size / _stride;
    }

    public static uint FindMemoryType(
        VkInstanceApi api,
        VkPhysicalDevice physicalDevice,
        uint typeFilter,
        VkMemoryPropertyFlags properties
    )
    {
        api.vkGetPhysicalDeviceMemoryProperties(physicalDevice, out var memProperties);

        for (int i = 0; i < memProperties.memoryTypeCount; i++)
        {
            if (
                (typeFilter & (1 << i)) != 0
                && (memProperties.memoryTypes[i].propertyFlags & properties) == properties
            )
            {
                return (uint)i;
            }
        }

        throw new Exception("failed to find suitable memory type!");
    }

    public class MemoryMap(VkDeviceApi vd, VkDevice device, VkDeviceMemory Memory, IntPtr Ptr)
        : IDisposable
    {
        private readonly VkDeviceApi _vd = vd;
        private readonly VkDevice _device = device;
        private readonly VkDeviceMemory _memory = Memory;
        private readonly nint _ptr = Ptr;

        public unsafe T* ToPointer<T>()
            where T : unmanaged
        {
            return (T*)_ptr.ToPointer();
        }

        public unsafe void Dispose()
        {
            // Span<MappedMemoryRange> range = stackalloc MappedMemoryRange[2];
            // range[0].SType = VK_STRUCTURE_TYPE_MappedMemoryRange;
            // range[0].Memory = Vertex.Memory;
            // range[0].Size = Vk.WholeSize;
            // range[1].SType = VK_STRUCTURE_TYPE_MappedMemoryRange;
            // range[1].Memory = Index.Memory;
            // range[1].Size = Vk.WholeSize;
            var range = new VkMappedMemoryRange
            {
                sType = VK_STRUCTURE_TYPE_MAPPED_MEMORY_RANGE,
                memory = _memory,
                size = VK_WHOLE_SIZE,
            };
            if (_vd.vkFlushMappedMemoryRanges(1, &range) != VK_SUCCESS)
            {
                throw new Exception($"Unable to flush memory to device");
            }
            _vd.vkUnmapMemory(_memory);
        }
    }

    public unsafe MemoryMap Map()
    {
        void* p;
        if (_vd.vkMapMemory(_memory, 0, VK_WHOLE_SIZE, 0, (void**)(&p)) != VK_SUCCESS)
        {
            throw new Exception($"Unable to map device memory");
        }
        return new MemoryMap(_vd, _device, _memory, new IntPtr(p));
    }

    public unsafe void Bind(VkCommandBuffer commandBuffer)
    {
        if (_usage.HasFlag(VkBufferUsageFlags.VertexBuffer))
        {
            ulong vertex_offset = 0;
            var buffer = Buffer;
            _vd.vkCmdBindVertexBuffers(commandBuffer, 0, 1, &buffer, &vertex_offset);
        }
        else if (_usage.HasFlag(VkBufferUsageFlags.IndexBuffer))
        {
            VkIndexType indexType;
            switch (_stride)
            {
                case 2:
                    indexType = VkIndexType.Uint16;
                    break;
                case 4:
                    indexType = VkIndexType.Uint32;
                    break;
                default:
                    throw new Exception();
            }

            _vd.vkCmdBindIndexBuffer(commandBuffer, Buffer, 0, indexType);
        }
        else
        {
            throw new Exception();
        }
    }

    public void Draw(VkCommandBuffer commandBuffer)
    {
        Draw(commandBuffer, 0, (uint)_itemCount);
    }

    public void Draw(VkCommandBuffer commandBuffer, uint offset, uint count)
    {
        if (_usage.HasFlag(VkBufferUsageFlags.VertexBuffer))
        {
            _vd.vkCmdDraw(commandBuffer, count, 1, offset, 0);
        }
        else if (_usage.HasFlag(VkBufferUsageFlags.IndexBuffer))
        {
            _vd.vkCmdDrawIndexed(commandBuffer, count, 1, offset, 0, 0);
        }
        else
        {
            throw new Exception();
        }
    }
}
