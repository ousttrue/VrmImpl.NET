using VrmImpl.Drawlist;
using shaderc;
using Vortice.Vulkan;
using static Vortice.Vulkan.Vulkan;

namespace VrmImpl.VorticeVulkan;

public static class VkHelper
{
    private static unsafe byte[] Compile(string src, string path, ShaderKind shaderKind)
    {
        using var comp = new Compiler();
        using var res = comp.Compile(src, path, shaderKind);
        if (res.Status != Status.Success)
        {
            throw new Exception(res.ErrorMessage);
        }
        return new ReadOnlySpan<byte>(res.CodePointer.ToPointer(), (int)res.CodeLength).ToArray();
    }

    public static (
        VkVertexInputBindingDescription,
        VkVertexInputAttributeDescription[]
    ) InputFromLayout(FloatVertexLayout[] layout)
    {
        VkVertexInputBindingDescription binding = new()
        {
            binding = 0,
            stride = layout[0].Stride,
            inputRate = VkVertexInputRate.Vertex,
        };
        var attribes = new VkVertexInputAttributeDescription[layout.Length];
        for (uint i = 0; i < layout.Length; ++i)
        {
            attribes[i] = new VkVertexInputAttributeDescription()
            {
                binding = 0,
                location = i,
                format = VkFormat.R32G32B32Sfloat,
                offset = layout[i].Offset,
            };
        }
        return (binding, attribes);
    }

    public static unsafe void AllocateDescriptorSets(
        VkDeviceApi vk,
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
            sType = VkStructureType.DescriptorSetAllocateInfo,
            descriptorPool = pool,
            descriptorSetCount = maxSets,
            pSetLayouts = layouts,
        };
        fixed (VkDescriptorSet* descriptorSetsPtr = descriptorSets)
        {
            vk.vkAllocateDescriptorSets(in allocateInfo, descriptorSetsPtr).ThrowIfError();
        }
    }

    public static unsafe VkCommandPool CreateCommandPool(
        VkDeviceApi vk,
        uint graphicsQueueFamilyIndex
    )
    {
        VkCommandPoolCreateInfo poolInfo = new()
        {
            sType = VkStructureType.CommandPoolCreateInfo,
            queueFamilyIndex = graphicsQueueFamilyIndex,
            flags = VkCommandPoolCreateFlags.ResetCommandBuffer,
        };
        vk.vkCreateCommandPool(in poolInfo, null, out var commandPool).ThrowIfError();
        return commandPool;
    }

    public static unsafe VkImageView CreateImageView(
        VkDeviceApi vk,
        VkImage image,
        VkFormat format,
        VkImageAspectFlags aspectFlags
    )
    {
        VkImageViewCreateInfo createInfo = new()
        {
            sType = VkStructureType.ImageViewCreateInfo,
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

        vk.vkCreateImageView(in createInfo, null, out var imageView).ThrowIfError();

        return imageView;
    }

    public static unsafe void CreateImage(
        VkInstanceApi vki,
        VkDeviceApi vkd,
        VkPhysicalDevice physicalDevice,
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
            sType = VkStructureType.ImageCreateInfo,
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
            vkd.vkCreateImage(in imageInfo, null, imagePtr).ThrowIfError();
        }

        vkd.vkGetImageMemoryRequirements(image, out VkMemoryRequirements memRequirements);

        VkMemoryAllocateInfo allocInfo = new()
        {
            sType = VkStructureType.MemoryAllocateInfo,
            allocationSize = memRequirements.size,
            memoryTypeIndex = FindMemoryType(
                vki,
                physicalDevice,
                memRequirements.memoryTypeBits,
                properties
            ),
        };

        fixed (VkDeviceMemory* imageMemoryPtr = &imageMemory)
        {
            vkd.vkAllocateMemory(&allocInfo, null, imageMemoryPtr).ThrowIfError();
        }

        vkd.vkBindImageMemory(image, imageMemory, 0);
    }

    public static uint FindMemoryType(
        VkInstanceApi vk,
        VkPhysicalDevice physicalDevice,
        uint typeFilter,
        VkMemoryPropertyFlags properties
    )
    {
        vk.vkGetPhysicalDeviceMemoryProperties(
            physicalDevice,
            out VkPhysicalDeviceMemoryProperties memProperties
        );

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

    public static unsafe void CreateBuffer(
        VkInstanceApi vki,
        VkDeviceApi vkd,
        VkPhysicalDevice physicalDevice,
        ulong size,
        VkBufferUsageFlags usage,
        VkMemoryPropertyFlags properties,
        out VkBuffer buffer,
        out VkDeviceMemory bufferMemory
    )
    {
        VkBufferCreateInfo bufferInfo = new()
        {
            sType = VkStructureType.BufferCreateInfo,
            size = size,
            usage = usage,
            sharingMode = VkSharingMode.Exclusive,
        };

        fixed (VkBuffer* bufferPtr = &buffer)
        {
            vkd.vkCreateBuffer(in bufferInfo, null, bufferPtr).ThrowIfError();
        }

        VkMemoryRequirements memRequirements = new();
        vkd.vkGetBufferMemoryRequirements(buffer, out memRequirements);

        VkMemoryAllocateInfo allocateInfo = new()
        {
            sType = VkStructureType.MemoryAllocateInfo,
            allocationSize = memRequirements.size,
            memoryTypeIndex = FindMemoryType(
                vki,
                physicalDevice,
                memRequirements.memoryTypeBits,
                properties
            ),
        };

        fixed (VkDeviceMemory* bufferMemoryPtr = &bufferMemory)
        {
            vkd.vkAllocateMemory(&allocateInfo, null, bufferMemoryPtr).ThrowIfError();
        }

        vkd.vkBindBufferMemory(buffer, bufferMemory, 0);
    }

    // var clearValues = new ClearValue[]
    // {
    //     new()
    //     {
    //         Color = new()
    //         {
    //             Float32_0 = 0,
    //             Float32_1 = 0,
    //             Float32_2 = 0,
    //             Float32_3 = 1,
    //         },
    //     },
    //     new()
    //     {
    //         DepthStencil = new() { Depth = 1, Stencil = 0 },
    //     },
    // };
    public static unsafe void BeginRenderPass(
        VkDeviceApi vk,
        VkCommandBuffer commandBuffer,
        uint i,
        VkDescriptorSet descriptorSet,
        VkExtent2D extent,
        VkClearValue[] clearValues,
        VkRenderPass renderPass,
        VkFramebuffer[] framebuffers
    )
    {
        fixed (VkClearValue* clearValuesPtr = clearValues)
        {
            VkRenderPassBeginInfo renderPassInfo = new()
            {
                sType = VkStructureType.RenderPassBeginInfo,
                renderPass = renderPass,
                framebuffer = framebuffers[i],
                renderArea = { offset = { x = 0, y = 0 }, extent = extent },
                clearValueCount = (uint)clearValues.Length,
                pClearValues = clearValuesPtr,
            };
            vk.vkCmdBeginRenderPass(commandBuffer, &renderPassInfo, VkSubpassContents.Inline);
        }
    }

    public static void EndRenderPass(VkDeviceApi vk, VkCommandBuffer commandBuffer)
    {
        vk.vkCmdEndRenderPass(commandBuffer);
    }

    public static unsafe void TransitionImageLayout(
        VkDeviceApi vk,
        VkCommandBuffer commandBuffer,
        VkImage image,
        VkImageLayout newLayout
    )
    {
        VkImageMemoryBarrier barrier = new()
        {
            sType = VkStructureType.ImageMemoryBarrier,
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

        vk.vkCmdPipelineBarrier(
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

    public static unsafe void CopyBufferToImage(
        VkDeviceApi vk,
        VkCommandBuffer commandBuffer,
        VkBuffer buffer,
        VkImage image,
        uint width,
        uint height
    )
    {
        VkBufferImageCopy region = new()
        {
            bufferOffset = 0,
            bufferRowLength = 0,
            bufferImageHeight = 0,
            imageSubresource =
            {
                aspectMask = VkImageAspectFlags.Color,
                mipLevel = 0,
                baseArrayLayer = 0,
                layerCount = 1,
            },
            imageOffset = new VkOffset3D(0, 0, 0),
            imageExtent = new VkExtent3D(width, height, 1),
        };

        vk.vkCmdCopyBufferToImage(
            commandBuffer,
            buffer,
            image,
            VkImageLayout.TransferDstOptimal,
            1,
            &region
        );
    }

    public static unsafe void CopyBuffer(
        VkDeviceApi vk,
        VkCommandBuffer commandBuffer,
        VkBuffer srcBuffer,
        VkBuffer dstBuffer,
        ulong size
    )
    {
        VkBufferCopy copyRegion = new() { size = size };
        vk.vkCmdCopyBuffer(commandBuffer, srcBuffer, dstBuffer, 1, &copyRegion);
    }
}
