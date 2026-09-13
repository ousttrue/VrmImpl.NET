using System.Reflection;
using System.Text;
using Microsoft.Extensions.Logging;
using Vortice.Vulkan;
using VrmImpl.Gui;
using VrmImpl.VorticeVulkan;
using static Vortice.Vulkan.Vulkan;

namespace VrmImpl;

static class Program
{
    public static byte[] FromAssembly(Assembly assm, string name)
    {
        using var stream =
            assm.GetManifestResourceStream(name)
            ?? throw new Exception($"GetManifestResourceStream: {name}");
        // var reader = new StreamReader(stream);
        // return reader.ReadToEnd();
        using (MemoryStream ms = new MemoryStream())
        {
            stream.CopyTo(ms);
            return ms.ToArray();
        }
    }

    public static unsafe void Main()
    {
        var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.ClearProviders();
            builder.AddColorConsoleLogger(configuration => { });
#if DEBUG
            builder.SetMinimumLevel(LogLevel.Debug);
#endif
        });
        VulkanLogger.Inject(loggerFactory);

        using var window = new GlfwWindow(loggerFactory, resizable: true);
        using var instance = new VulkanInstanceObject(window.GetVkExtensions());
        var surface = window.CreateVkSurface(instance.Instance.Handle);
        using var disposer = new ActionDisposer(() =>
        {
            instance.Api.vkDestroySurfaceKHR(surface);
        });

        ReadOnlySpan<string> DeviceExtensions =
        [
            Encoding.ASCII.GetString(VK_KHR_SWAPCHAIN_EXTENSION_NAME),
        ];
        var picked = VulkanPhysicalDeviceInfo.Pick(instance.Api, DeviceExtensions, surface);
        var indices = VulkanQueueFamilyIndices.findQueueFamilies(instance.Api, picked, surface);
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
            surface,
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

        var assm = Assembly.GetExecutingAssembly();
        var vs = FromAssembly(assm, "shader.vert.spv");
        var fs = FromAssembly(assm, "shader.frag.spv");

        using var pipeline = new VulkanPipelineObject(
            device.Api,
            swapchain.Format,
            // renderTarget.RenderPass
            null,
            vs,
            fs
        );

        var resizeSwapchain = false;
        while (true)
        {
            if (window.NewFrame() is not (int fb_width, int fb_height))
            {
                break;
            }
            var extent = new VkExtent2D(fb_width, fb_height);
            if (resizeSwapchain || swapchain.ShouldRecreate(extent))
            {
                device.Api.vkDeviceWaitIdle();
                renderTarget.Dispose();
                swapchain.Resize(extent);
                renderTarget.Create(swapchain.Format, extent, swapchain.Images);
                continue;
            }
            if (window.IsIconified())
            {
                Thread.Sleep(10);
                continue;
            }

            if (
                swapchain.Acquire()
                is not
                (
                    uint imageIndex,
                    VkSemaphore imageAvailableSemaphore,
                    VkSemaphore renderFinishedSemaphore,
                    VkFence inFlightFence
                )
            )
            {
                resizeSwapchain = true;
                continue;
            }

            VkClearValue clearColor = default;
            clearColor.color.float32[0] = 0.0f;
            clearColor.color.float32[1] = 0.0f;
            clearColor.color.float32[2] = 0.0f;
            clearColor.color.float32[2] = 1.0f;
            // var (commandBuffer, renderFinishedSemaphore) = renderTarget.BeginRenderPass(
            //     imageIndex,
            //     clearValues
            // );
            var commandBuffer = renderTarget.BeginRendering(
                imageIndex,
                swapchain.Images[imageIndex],
                swapchain.Extent,
                [clearColor]
            );
            {
                pipeline.RecordCommandBuffer(commandBuffer);
            }
            // renderTarget.EndRenderPass();
            renderTarget.EndRendering(swapchain.Images[imageIndex]);
            renderTarget.EndSubmitCommandBuffer(
                imageAvailableSemaphore,
                renderFinishedSemaphore,
                inFlightFence
            );

            swapchain.Present(imageIndex, renderFinishedSemaphore);
        }

        device.Api.vkDeviceWaitIdle().ThrowIfError();
    }
}
