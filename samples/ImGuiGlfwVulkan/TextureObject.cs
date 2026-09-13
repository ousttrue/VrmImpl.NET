using System.Runtime.CompilerServices;
using Vortice.Vulkan;
using static Vortice.Vulkan.Vulkan;

namespace VrmImpl;

public class TextureObject : IDisposable
{
    private readonly VkInstanceApi _vi;
    private readonly VkDeviceApi _vd;
    private readonly VkDevice _device;
    public readonly uint Width;
    public readonly uint Height;
    public readonly VkImage Image;
    private readonly VkDeviceMemory _memory;
    public readonly VkImageView ImageView;
    public readonly VkSampler Sampler;

    public unsafe TextureObject(
        VkInstanceApi vi,
        VkDeviceApi vd,
        VkPhysicalDevice physicalDevice,
        VkDevice device,
        uint width,
        uint height,
        VkImageUsageFlags usage
    )
    {
        _vi = vi;
        _vd = vd;
        _device = device;
        Width = width;
        Height = height;

        CreateImage(
            vi,
            _vd,
            physicalDevice,
            _device,
            width,
            height,
            VkFormat.R8G8B8A8Unorm,
            VkImageTiling.Optimal,
            usage, //ImageUsageFlags.SampledBit | ImageUsageFlags.TransferDstBit,
            VkMemoryPropertyFlags.DeviceLocal,
            out Image,
            out _memory
        );

        ImageView = CreateImageView(
            _vd,
            _device,
            Image,
            VkFormat.R8G8B8A8Unorm,
            VkImageAspectFlags.Color
        );

        var info = new VkSamplerCreateInfo
        {
            sType = VK_STRUCTURE_TYPE_SAMPLER_CREATE_INFO,
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
        if (_vd.vkCreateSampler(in info, default, out Sampler) != VK_SUCCESS)
        {
            throw new Exception($"Unable to create sampler");
        }
    }

    public static unsafe VkImageView CreateImageView(
        VkDeviceApi vd,
        VkDevice device,
        VkImage image,
        VkFormat format,
        VkImageAspectFlags aspectFlags
    )
    {
        VkImageViewCreateInfo createInfo = new()
        {
            sType = VK_STRUCTURE_TYPE_IMAGE_VIEW_CREATE_INFO,
            image = image,
            viewType = VkImageViewType.Image2D,
            format = format,
            //Components =
            //    {
            //        R = ComponentSwizzle.Identity,
            //        G = ComponentSwizzle.Identity,
            //        B = ComponentSwizzle.Identity,
            //        A = ComponentSwizzle.Identity,
            //    },
            subresourceRange =
            {
                aspectMask = aspectFlags,
                baseMipLevel = 0,
                levelCount = 1,
                baseArrayLayer = 0,
                layerCount = 1,
            },
        };

        if (vd.vkCreateImageView(in createInfo, null, out var imageView) != VK_SUCCESS)
        {
            throw new Exception("failed to create image views!");
        }

        return imageView;
    }

    public static unsafe void CreateImage(
        VkInstanceApi vi,
        VkDeviceApi vd,
        VkPhysicalDevice physicalDevice,
        VkDevice device,
        uint width,
        uint height,
        VkFormat format,
        VkImageTiling tiling,
        VkImageUsageFlags usage,
        VkMemoryPropertyFlags properties,
        out VkImage image,
        out VkDeviceMemory imageMemory
    )
    {
        VkImageCreateInfo imageInfo = new()
        {
            sType = VK_STRUCTURE_TYPE_IMAGE_CREATE_INFO,
            imageType = VkImageType.Image2D,
            extent =
            {
                width = width,
                height = height,
                depth = 1,
            },
            mipLevels = 1,
            arrayLayers = 1,
            format = format,
            tiling = tiling,
            initialLayout = VkImageLayout.Undefined,
            usage = usage,
            samples = VkSampleCountFlags.Count1,
            sharingMode = VkSharingMode.Exclusive,
        };

        fixed (VkImage* imagePtr = &image)
        {
            if (vd.vkCreateImage(in imageInfo, null, imagePtr) != VK_SUCCESS)
            {
                throw new Exception("failed to create image!");
            }
        }

        vd.vkGetImageMemoryRequirements(image, out var memRequirements);

        VkMemoryAllocateInfo allocInfo = new()
        {
            sType = VK_STRUCTURE_TYPE_MEMORY_ALLOCATE_INFO,
            allocationSize = memRequirements.size,
            memoryTypeIndex = FindMemoryType(
                vi,
                physicalDevice,
                memRequirements.memoryTypeBits,
                properties
            ),
        };

        fixed (VkDeviceMemory* imageMemoryPtr = &imageMemory)
        {
            if (vd.vkAllocateMemory(&allocInfo, null, imageMemoryPtr) != VK_SUCCESS)
            {
                throw new Exception("failed to allocate image memory!");
            }
        }

        vd.vkBindImageMemory(image, imageMemory, 0);
    }

    public static uint FindMemoryType(
        VkInstanceApi vi,
        VkPhysicalDevice physicalDevice,
        uint typeFilter,
        VkMemoryPropertyFlags properties
    )
    {
        vi.vkGetPhysicalDeviceMemoryProperties(physicalDevice, out var memProperties);

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

    public unsafe TextureObject(
        VkInstanceApi vi,
        VkDeviceApi vd,
        VkPhysicalDevice physicalDevice,
        VkDevice device,
        uint width,
        uint height,
        uint graphicsQueueFamilyIndex,
        ReadOnlySpan<byte> pixels
    )
        : this(
            vi,
            vd,
            physicalDevice,
            device,
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
        _vd.vkDestroySampler(Sampler, default);
        _vd.vkDestroyImageView(ImageView, default);
        _vd.vkDestroyImage(Image, default);
        _vd.vkFreeMemory(_memory, default);
    }

    public unsafe void Upload(
        VkPhysicalDevice physicalDevice,
        uint graphicsQueueFamilyIndex,
        IntPtr pixels
    )
    {
        var upload_size = (ulong)(Width * Height * 4 * sizeof(byte));

        CreateBuffer(
            _vi,
            _vd,
            physicalDevice,
            _device,
            upload_size,
            VkBufferUsageFlags.TransferSrc,
            VkMemoryPropertyFlags.HostVisible,
            out VkBuffer uploadBuffer,
            out VkDeviceMemory uploadBufferMemory
        );
        void* map = null;
        if (_vd.vkMapMemory(uploadBufferMemory, 0, upload_size, 0, (void**)(&map)) != VK_SUCCESS)
        {
            throw new Exception($"Failed to map device memory");
        }
        Unsafe.CopyBlock(map, pixels.ToPointer(), (uint)upload_size);
        var range = new VkMappedMemoryRange
        {
            sType = VK_STRUCTURE_TYPE_MAPPED_MEMORY_RANGE,
            memory = uploadBufferMemory,
            size = upload_size,
        };
        if (_vd.vkFlushMappedMemoryRanges(1, &range) != VK_SUCCESS)
        {
            throw new Exception($"Failed to flush memory to device");
        }
        _vd.vkUnmapMemory(uploadBufferMemory);

        using var ot = new OneTimeCommandBuffer(_vd, graphicsQueueFamilyIndex);
        ot.Execute(commandBuffer =>
        {
            TransitionImageLayout(_vd, commandBuffer, Image, VkImageLayout.TransferDstOptimal);

            var region = new VkBufferImageCopy
            {
                imageSubresource = new() { aspectMask = VkImageAspectFlags.Color, layerCount = 1 },
                imageExtent = new()
                {
                    width = Width,
                    height = Height,
                    depth = 1,
                },
            };
            _vd.vkCmdCopyBufferToImage(
                commandBuffer,
                uploadBuffer,
                Image,
                VkImageLayout.TransferDstOptimal,
                1,
                &region
            );

            TransitionImageLayout(_vd, commandBuffer, Image, VkImageLayout.ShaderReadOnlyOptimal);
        });
        _vd.vkDestroyBuffer(uploadBuffer, default);
        _vd.vkFreeMemory(uploadBufferMemory, default);
    }

    public static unsafe void TransitionImageLayout(
        VkDeviceApi vd,
        VkCommandBuffer commandBuffer,
        VkImage image,
        VkImageLayout newLayout
    )
    {
        VkImageMemoryBarrier barrier = new()
        {
            sType = VK_STRUCTURE_TYPE_IMAGE_MEMORY_BARRIER,
            oldLayout = VkImageLayout.Undefined,
            newLayout = newLayout,
            srcQueueFamilyIndex = VK_QUEUE_FAMILY_IGNORED,
            dstQueueFamilyIndex = VK_QUEUE_FAMILY_IGNORED,
            image = image,
            subresourceRange =
            {
                aspectMask = VkImageAspectFlags.Color,
                baseMipLevel = 0,
                levelCount = 1,
                baseArrayLayer = 0,
                layerCount = 1,
            },
        };

        vd.vkCmdPipelineBarrier(
            commandBuffer,
            VkPipelineStageFlags.BottomOfPipe,
            VkPipelineStageFlags.TopOfPipe,
            0,
            0,
            null,
            0,
            null,
            1,
            &barrier
        );
    }

    public static unsafe void CreateBuffer(
        VkInstanceApi vi,
        VkDeviceApi vd,
        VkPhysicalDevice physicalDevice,
        VkDevice device,
        ulong size,
        VkBufferUsageFlags usage,
        VkMemoryPropertyFlags properties,
        out VkBuffer buffer,
        out VkDeviceMemory bufferMemory
    )
    {
        VkBufferCreateInfo bufferInfo = new()
        {
            sType = VK_STRUCTURE_TYPE_BUFFER_CREATE_INFO,
            size = size,
            usage = usage,
            sharingMode = VkSharingMode.Exclusive,
        };

        fixed (VkBuffer* bufferPtr = &buffer)
        {
            if (vd.vkCreateBuffer(in bufferInfo, null, bufferPtr) != VK_SUCCESS)
            {
                throw new Exception("failed to create vertex buffer!");
            }
        }

        VkMemoryRequirements memRequirements = new();
        vd.vkGetBufferMemoryRequirements(buffer, out memRequirements);

        VkMemoryAllocateInfo allocateInfo = new()
        {
            sType = VK_STRUCTURE_TYPE_MEMORY_ALLOCATE_INFO,
            allocationSize = memRequirements.size,
            memoryTypeIndex = FindMemoryType(
                vi,
                physicalDevice,
                memRequirements.memoryTypeBits,
                properties
            ),
        };

        fixed (VkDeviceMemory* bufferMemoryPtr = &bufferMemory)
        {
            if (vd.vkAllocateMemory(&allocateInfo, null, bufferMemoryPtr) != VK_SUCCESS)
            {
                throw new Exception("failed to allocate vertex buffer memory!");
            }
        }

        vd.vkBindBufferMemory(buffer, bufferMemory, 0);
    }
}
