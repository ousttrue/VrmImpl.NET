// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;
using Vortice.Vulkan;
using static Vortice.Vulkan.Vulkan;

namespace VrmImpl.VorticeVulkan;

public class ArrayBufferObject(
    VkInstanceApi vki,
    VkDeviceApi vkd,
    VkBufferUsageFlags usage,
    VkMemoryPropertyFlags memoryProps,
    uint stride
) : IDisposable
{
    private readonly VkInstanceApi _vki = vki;
    private readonly VkDeviceApi _vkd = vkd;
    private readonly VkBufferUsageFlags _usage = usage;
    private readonly VkMemoryPropertyFlags _memoryProps = memoryProps;
    private uint _stride = stride;
    private ulong _itemCount = 0;
    public ulong ByteLength => _stride * _itemCount;
    public VkBuffer Buffer;
    private VkDeviceMemory _memory;
    private ulong _bufferMemoryAlignment = 256;

    public ArrayBufferObject(
        VkInstanceApi vki,
        VkDeviceApi vkd,
        VkBufferUsageFlags usage,
        VkMemoryPropertyFlags memoryProps,
        VkPhysicalDevice physicalDevice,
        uint stride,
        ulong itemCount
    )
        : this(vki, vkd, usage, memoryProps, stride)
    {
        Grow(physicalDevice, itemCount);
    }

    /// <summary>
    /// use MemoryPropertyFlags.HostVisibleBit and map
    /// </summary>
    public static unsafe ArrayBufferObject Create<T>(
        VkInstanceApi vki,
        VkDeviceApi vkd,
        VkBufferUsageFlags usage,
        VkPhysicalDevice physicalDevice,
        ReadOnlySpan<T> values
    )
        where T : unmanaged
    {
        var self = new ArrayBufferObject(vki,
            vkd,
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
        VkInstanceApi vki,
        VkDeviceApi vkd,
        VkBufferUsageFlags usage,
        VkPhysicalDevice physicalDevice,
        uint graphicsQueueFamilyIndex,
        uint stride,
        ReadOnlySpan<byte> data
    )
    {
        var self = new ArrayBufferObject(vki,
            vkd,
            usage,
            VkMemoryPropertyFlags.DeviceLocal,
            physicalDevice,
            stride,
            (uint)(data.Length / stride)
        );
        using var singleTimeCommand = new OneTimeCommandBuffer(vkd, graphicsQueueFamilyIndex);
        using (var staging = Create(vki, vkd, VkBufferUsageFlags.TransferSrc, physicalDevice, data))
        {
            singleTimeCommand.Execute(commandBuffer =>
            {
                VkHelper.CopyBuffer(
                    vkd,
                    commandBuffer,
                    staging.Buffer,
                    self.Buffer,
                    staging.ByteLength
                );
            });
        }
        return self;
    }

    public static ArrayBufferObject Create<T>(
        VkInstanceApi vki,
        VkDeviceApi vkd,
        VkBufferUsageFlags usage,
        VkPhysicalDevice physicalDevice,
        uint graphicsQueueFamilyIndex,
        ReadOnlySpan<T> values
    )
        where T : unmanaged
    {
        return Create(vki,
            vkd,
            usage,
            physicalDevice,
            graphicsQueueFamilyIndex,
            (uint)Marshal.SizeOf<T>(),
            MemoryMarshal.Cast<T, byte>(values)
        );
    }

    public unsafe void Dispose()
    {
        _vkd.vkDestroyBuffer(Buffer, default);
        _vkd.vkFreeMemory(_memory, default);
    }

    public unsafe void Grow(VkPhysicalDevice physicalDevice, ulong itemCount)
    {
        if (Buffer.Handle != default && _itemCount >= itemCount)
        {
            return;
        }

        if (Buffer.Handle != default)
        {
            _vkd.vkDestroyBuffer(Buffer, default);
        }
        if (_memory.Handle != default)
        {
            _vkd.vkFreeMemory(_memory, default);
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
            sType = VkStructureType.BufferCreateInfo,
            size = sizeAlignedVertexBuffer,
            usage = _usage,
            sharingMode = VkSharingMode.Exclusive,
        };
        _vkd.vkCreateBuffer(in bufferInfo, default, out Buffer).ThrowIfError();

        _vkd.vkGetBufferMemoryRequirements(Buffer, out var req);
        _bufferMemoryAlignment =
            (_bufferMemoryAlignment > req.alignment) ? _bufferMemoryAlignment : req.alignment;
        VkMemoryAllocateInfo allocInfo = new VkMemoryAllocateInfo
        {
            sType = VkStructureType.MemoryAllocateInfo,
            allocationSize = req.size,
            memoryTypeIndex = VkHelper.FindMemoryType(
                _vki,
                physicalDevice,
                req.memoryTypeBits,
                _memoryProps
            ),
        };
        _vkd.vkAllocateMemory(&allocInfo, default, out _memory).ThrowIfError();

        _vkd.vkBindBufferMemory(Buffer, _memory, 0).ThrowIfError();
        _itemCount = req.size / _stride;
    }

    public class MemoryMap(VkDeviceApi vk, VkDeviceMemory Memory, IntPtr Ptr) : IDisposable
    {
        private readonly VkDeviceApi _vk = vk;
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
            // range[0].sType = VkStructureType.MappedMemoryRange;
            // range[0].Memory = Vertex.Memory;
            // range[0].Size = Vk.WholeSize;
            // range[1].sType = VkStructureType.MappedMemoryRange;
            // range[1].Memory = Index.Memory;
            // range[1].Size = Vk.WholeSize;
            var range = new VkMappedMemoryRange
            {
                sType = VkStructureType.MappedMemoryRange,
                memory = _memory,
                size = VK_WHOLE_SIZE,
            };
            _vk.vkFlushMappedMemoryRanges(1, &range).ThrowIfError();
            _vk.vkUnmapMemory(_memory);
        }
    }

    public unsafe MemoryMap Map()
    {
        void* p;
        _vkd.vkMapMemory(_memory, 0, VK_WHOLE_SIZE, 0, (void**)(&p)).ThrowIfError();
        return new MemoryMap(_vkd, _memory, new IntPtr(p));
    }

    public unsafe void Bind(VkCommandBuffer commandBuffer)
    {
        if (_usage.HasFlag(VkBufferUsageFlags.VertexBuffer))
        {
            ulong vertex_offset = 0;
            var buffer = Buffer;
            _vkd.vkCmdBindVertexBuffers(commandBuffer, 0, 1, &buffer, &vertex_offset);
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

            _vkd.vkCmdBindIndexBuffer(commandBuffer, Buffer, 0, indexType);
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
            _vkd.vkCmdDraw(commandBuffer, count, 1, offset, 0);
        }
        else if (_usage.HasFlag(VkBufferUsageFlags.IndexBuffer))
        {
            _vkd.vkCmdDrawIndexed(commandBuffer, count, 1, offset, 0, 0);
        }
        else
        {
            throw new Exception();
        }
    }
}
