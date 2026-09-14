// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;
using Vortice.Vulkan;

namespace VrmImpl.VorticeVulkan;

public class TextureObject : IDisposable
{
    private readonly VkInstanceApi _vki;
    private readonly VkDeviceApi _vkd;
    public readonly uint Width;
    public readonly uint Height;
    public readonly VkImage Image;
    private readonly VkDeviceMemory _memory;
    public readonly VkImageView ImageView;
    public readonly VkSampler Sampler;

    public unsafe TextureObject(
        VkInstanceApi vki,
        VkDeviceApi vkd,
        VkPhysicalDevice physicalDevice,
        uint width,
        uint height,
        VkImageUsageFlags usage
    )
    {
        _vki = vki;
        _vkd = vkd;
        Width = width;
        Height = height;

        VkHelper.CreateImage(
            _vki,
            _vkd,
            physicalDevice,
            width,
            height,
            VkFormat.R8G8B8A8Unorm,
            VkImageTiling.Optimal,
            usage, //ImageUsageFlags.SampledBit | ImageUsageFlags.TransferDstBit,
            VkMemoryPropertyFlags.DeviceLocal,
            out Image,
            out _memory
        );

        ImageView = VkHelper.CreateImageView(
            _vkd,
            Image,
            VkFormat.R8G8B8A8Unorm,
            VkImageAspectFlags.Color
        );

        var info = new VkSamplerCreateInfo
        {
            sType = VkStructureType.SamplerCreateInfo,
            magFilter = VkFilter.Linear,
            minFilter = VkFilter.Linear,
            mipmapMode = VkSamplerMipmapMode.Linear,
            addressModeU = VkSamplerAddressMode.Repeat,
            addressModeV = VkSamplerAddressMode.Repeat,
            addressModeW = VkSamplerAddressMode.Repeat,
            minLod = -1000,
            maxLod = 1000,
            maxAnisotropy = 1.0f,
        };
        vkd.vkCreateSampler(in info, default, out Sampler).ThrowIfError();
    }

    public unsafe TextureObject(
        VkInstanceApi vki,
        VkDeviceApi vkd,
        VkPhysicalDevice physicalDevice,
        uint width,
        uint height,
        uint graphicsQueueFamilyIndex,
        ReadOnlySpan<byte> pixels
    )
        : this(
            vki,
            vkd,
            physicalDevice,
            width,
            height,
            VkImageUsageFlags.Sampled | VkImageUsageFlags.TransferDst
        )
    {
        fixed (void* p = pixels)
        {
            Upload(physicalDevice, graphicsQueueFamilyIndex, new nint(p));
        }
    }

    public unsafe void Dispose()
    {
        _vkd.vkDestroySampler(Sampler, default);
        _vkd.vkDestroyImageView(ImageView, default);
        _vkd.vkDestroyImage(Image, default);
        _vkd.vkFreeMemory(_memory, default);
    }

    public unsafe void Upload(
        VkPhysicalDevice physicalDevice,
        uint graphicsQueueFamilyIndex,
        IntPtr pixels
    )
    {
        var upload_size = (ulong)(Width * Height * 4 * sizeof(byte));

        VkHelper.CreateBuffer(
            _vki,
            _vkd,
            physicalDevice,
            upload_size,
            VkBufferUsageFlags.TransferSrc,
            VkMemoryPropertyFlags.HostVisible,
            out var uploadBuffer,
            out var uploadBufferMemory
        );
        void* map = null;
        _vkd.vkMapMemory(uploadBufferMemory, 0, upload_size, 0, (void**)(&map)).ThrowIfError();

        Unsafe.CopyBlock(map, pixels.ToPointer(), (uint)upload_size);
        var range = new VkMappedMemoryRange
        {
            sType = VkStructureType.MappedMemoryRange,
            memory = uploadBufferMemory,
            size = upload_size,
        };
        _vkd.vkFlushMappedMemoryRanges(1, &range).ThrowIfError();
        _vkd.vkUnmapMemory(uploadBufferMemory);

        using var ot = new OneTimeCommandBuffer(_vkd, graphicsQueueFamilyIndex);
        ot.Execute(commandBuffer =>
        {
            VkHelper.TransitionImageLayout(
                _vkd,
                commandBuffer,
                Image,
                VkImageLayout.TransferDstOptimal
            );

            var region = new VkBufferImageCopy
            {
                imageSubresource = new VkImageSubresourceLayers
                {
                    aspectMask = VkImageAspectFlags.Color,
                    layerCount = 1,
                },
                imageExtent = new VkExtent3D
                {
                    width = Width,
                    height = Height,
                    depth = 1,
                },
            };
            _vkd.vkCmdCopyBufferToImage(
                commandBuffer,
                uploadBuffer,
                Image,
                VkImageLayout.TransferDstOptimal,
                1,
                &region
            );

            VkHelper.TransitionImageLayout(
                _vkd,
                commandBuffer,
                Image,
                VkImageLayout.ShaderReadOnlyOptimal
            );
        });
        _vkd.vkDestroyBuffer(uploadBuffer, default);
        _vkd.vkFreeMemory(uploadBufferMemory, default);
    }
}
