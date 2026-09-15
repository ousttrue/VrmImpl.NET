using System.Numerics;
using System.Reflection;
using System.Text;
using ImGuiNET;
using Microsoft.Extensions.Logging;
using Vortice.Vulkan;
using VrmImpl.Gui;
using VrmImpl.SceneGraph;
using VrmImpl.VorticeVulkan;
using static Vortice.Vulkan.Vulkan;

namespace VrmImpl;

internal class Program
{
    private static unsafe void Main(string[] args)
    {
        Scene? Model = default;
        if (args.Length >= 1)
        {
            Model = Scene.LoadFilePath(args[0]);
        }
        Scene? Motion = default;
        if (args.Length >= 2)
        {
            Motion = Scene.LoadFilePath(args[1]);
        }

        var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.ClearProviders();
            builder.AddColorConsoleLogger(configuration => { });
#if DEBUG
            builder.SetMinimumLevel(LogLevel.Debug);
#endif
        });

        VulkanLogger.Inject(loggerFactory);
        using var window = new GlfwWindow(loggerFactory, 1024, 768, "VrmViewer", resizable: true);
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
        using var renderTarget = new VulkanRenderTargetObject(
            device.Api,
            indices.GraphicsFamily,
            swapchain.Format,
            swapchain.Extent,
            swapchain.Images
        );

        using var imgui_context = new ImGuiContext();
        var io = ImGui.GetIO();
        io.ConfigFlags |= ImGuiConfigFlags.DockingEnable;
        using var implGlfw = ImGuiImplGlfw.InitForVulkan(window.WindowHandle, true);

        var assm = Assembly.GetExecutingAssembly();
        var vs = FromAssembly(assm, "glsl_shader.vert.spv");
        var fs = FromAssembly(assm, "glsl_shader.frag.spv");
        using var implVulkan = new ImGuiImplVulkan(
            instance.Api,
            device.Api,
            device.Device,
            swapchain.Format,
            (uint)swapchain.Images.Length,
            vs,
            fs
        );
        // Build texture atlas
        IntPtr pixels;
        io.Fonts.GetTexDataAsRGBA32(out pixels, out var font_width, out var font_height); // Load as RGBA 32-bit (75% of the memory is wasted, but default font is so small) because it is more likely to be compatible with user's existing shaders. If your ImTextureId represent a higher-level concept than just a GL texture id, consider calling GetTexDataAsAlpha8() instead to save on GPU memory.
        // var fontBitmap = ig.GetFontBitmap();
        using var igFontTexture = new TextureObject(
            instance.Api,
            device.Api,
            picked,
            (uint)font_width,
            (uint)font_height,
            VkImageUsageFlags.Sampled | VkImageUsageFlags.TransferDst
        );
        igFontTexture.Upload(picked, device.GraphicsQueueFamilyIndex, pixels);
        var fontDesc = implVulkan.BindTexture(igFontTexture);
        implVulkan.SetFontTexture(fontDesc);

        using var dockManager = new DockSceneRenderer(
            instance.Api,
            device.Api,
            picked,
            device.GraphicsQueueFamilyIndex,
            (uint)swapchain.Images.Length,
            implVulkan
        );

        // Our state
        var clear_color = new Vector4(0.60f, 0.45f, 0.55f, 1.00f);

        var resized = false;
        while (true)
        {
            if (window.NewFrame() is not (int fb_width, int fb_height))
            {
                break;
            }
            var extent = new VkExtent2D(fb_width, fb_height);
            if (resized || swapchain.ShouldRecreate(extent))
            {
                device.Api.vkDeviceWaitIdle();
                renderTarget.Dispose();
                swapchain.Resize(extent);
                renderTarget.Create(swapchain.Format, extent, swapchain.Images);
                resized = false;
            }
            if (window.IsIconified())
            {
                Thread.Sleep(10);
                continue;
            }

            // Start the Dear ImGui frame
            implGlfw.NewFrame();
            ImGui.NewFrame();

            // bool is_minimized = (
            //     draw_data.DisplaySize.X <= 0.0f || draw_data.DisplaySize.Y <= 0.0f
            // );
            // if (!is_minimized)
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
                resized = true;
                continue;
            }

            // render scenes to renderTexture
            dockManager.BeginFrame();
            if (Model is Scene modelScene)
            {
                modelScene.AddDelta(io.DeltaTime);
                dockManager.SetScene(modelScene);
            }
            if (Motion is Scene motionScene)
            {
                motionScene.AddDelta(io.DeltaTime);
                dockManager.SetScene(motionScene);
            }
            var renderTargetEnds = dockManager.EndFrame(imageIndex);

            //
            // Rendering
            //
            ImGui.Render();
            var draw_data = ImGui.GetDrawData();

            VkClearValue clearColor = default;
            clearColor.color.float32[0] = clear_color.X * clear_color.W;
            clearColor.color.float32[1] = clear_color.Y * clear_color.W;
            clearColor.color.float32[2] = clear_color.Z * clear_color.W;
            clearColor.color.float32[3] = clear_color.W;
            var commandBuffer = renderTarget.BeginRendering(
                imageIndex,
                swapchain.Images[imageIndex],
                swapchain.Extent,
                [clearColor]
            );

            {
                implVulkan.RenderImDrawData(
                    picked,
                    draw_data,
                    commandBuffer,
                    imageIndex,
                    new((uint)fb_width, (uint)fb_height)
                );
            }
            renderTarget.EndRendering(swapchain.Images[imageIndex]);
            renderTarget.EndSubmitCommandBuffer(
                [imageAvailableSemaphore, .. renderTargetEnds],
                renderFinishedSemaphore,
                inFlightFence
            );

            if (!swapchain.Present(imageIndex, renderFinishedSemaphore))
            {
                resized = true;
            }

            if (!io.WantCaptureKeyboard)
            {
                if (ImGui.IsKeyDown(ImGuiKey.Escape) || ImGui.IsKeyDown(ImGuiKey.Q))
                {
                    break;
                }
            }
        }

        // Cleanup
        device.Api.vkDeviceWaitIdle().ThrowIfError();
    }

    private static byte[] FromAssembly(Assembly assm, string name)
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
}
