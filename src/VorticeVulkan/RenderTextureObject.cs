using Vortice.Vulkan;

namespace VrmImpl.VorticeVulkan;

public class RenderTextureObject : IDisposable
{
    public TextureObject[] Textures;
    public RenderTargetObject RenderTarget;

    public RenderTextureObject(
        VkInstanceApi vki,
        VkDeviceApi vkd,
        VkPhysicalDevice physicalDevice,
        uint graphicsQueueFamilyIndex,
        uint imageCount,
        VkExtent2D extent
    )
    {
        using var ot = new OneTimeCommandBuffer(vkd, graphicsQueueFamilyIndex);

        Textures = new TextureObject[imageCount];
        for (int i = 0; i < Textures.Length; ++i)
        {
            Textures[i] = new(
                vki,
                vkd,
                physicalDevice,
                extent.width,
                extent.height,
                VkImageUsageFlags.Sampled | VkImageUsageFlags.ColorAttachment
            );
            ot.Execute(commandBuffer =>
            {
                VkHelper.TransitionImageLayout(
                    vkd,
                    commandBuffer,
                    Textures[i].Image,
                    VkImageLayout.ColorAttachmentOptimal
                );
            });
        }

        RenderTarget = new(
            vki,
            vkd,
            physicalDevice,
            graphicsQueueFamilyIndex,
            extent,
            VkFormat.R8G8B8A8Unorm,
            Textures.Select(x => x.Image).ToArray(),
            VkFormat.D24UnormS8Uint
        );
    }

    public void Dispose()
    {
        RenderTarget.Dispose();
        foreach (var texture in Textures)
        {
            texture.Dispose();
        }
    }
}
