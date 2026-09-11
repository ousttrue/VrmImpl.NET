using System.Text;
using Vortice.Vulkan;
using static Vortice.Vulkan.Vulkan;

namespace VrmImpl;

static class Program
{
    static readonly string[] DeviceExtensions =
    [
        Encoding.ASCII.GetString(VK_KHR_SWAPCHAIN_EXTENSION_NAME),
    ];

    public static unsafe void Main()
    {
        using var window = new GlfwWindow();
        using var instance = new VulkanInstanceObject(window);
        var picked = VulkanPhysicalDeviceInfo.Pick(
            instance.Api,
            DeviceExtensions,
            instance.Surface
        );

        var indices = VulkanQueueFamilyIndices.findQueueFamilies(
            instance.Api,
            picked,
            instance.Surface
        );
        using var device = new VulkanDeviceObject(
            instance.Api,
            picked,
            indices.GraphicsFamily,
            indices.PresentFamily,
            DeviceExtensions
        );

        var (w, h) = window.GetExtent();
        using var swapchain = new VulkanSwapchainObject(
            instance.Api,
            picked,
            instance.Surface,
            device.Api,
            new(w, h)
        );
        using var renderTarget = new VulkanRenderTarget(
            device.Api,
            indices.GraphicsFamily,
            swapchain.Format,
            swapchain.Extent,
            swapchain.Images
        );
        using var pipeline = new VulkanPipelineObject(
            device.Api,
            swapchain.Format,
            // renderTarget.RenderPass
            null
        );

        while (true)
        {
            if (!window.NextFrame())
            {
                break;
            }
            var (imageIndex, imageAvailableSemaphore, renderFinishedSemaphore, inFlightFence) =
                swapchain.Acquire();

            VkClearColorValue clearColor = default;
            clearColor.float32[0] = 0.0f;
            clearColor.float32[1] = 0.0f;
            clearColor.float32[2] = 0.0f;
            clearColor.float32[2] = 1.0f;
            ReadOnlySpan<VkClearValue> clearValues = [new VkClearValue { color = clearColor }];
            // var (commandBuffer, renderFinishedSemaphore) = renderTarget.BeginRenderPass(
            //     imageIndex,
            //     clearValues
            // );
            var commandBuffer = renderTarget.BeginRendering(
                imageIndex,
                swapchain.Images[imageIndex],
                swapchain.Extent,
                clearValues
            );
            {
                pipeline.RecordCommandBuffer(commandBuffer);
            }
            // renderTarget.EndRenderPass();
            renderTarget.EndRendering(swapchain.Images[imageIndex]);
            renderTarget.vkEndSubmitCommandBuffer(
                imageAvailableSemaphore,
                renderFinishedSemaphore,
                inFlightFence
            );

            swapchain.Present(imageIndex, renderFinishedSemaphore);
        }

        device.Api.vkDeviceWaitIdle();
    }
}
