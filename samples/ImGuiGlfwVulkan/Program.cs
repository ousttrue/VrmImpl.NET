// https://github.com/ocornut/imgui/blob/master/examples/example_glfw_vulkan/main.cpp

using System.Numerics;
using System.Reflection;
using System.Text;
using ImGuiNET;
using Microsoft.Extensions.Logging;
using Vortice.Vulkan;
using VrmImpl.Gui;
using VrmImpl.VorticeVulkan;
using static Vortice.Vulkan.Vulkan;

namespace VrmImpl;

static unsafe class Program
{
    public static int Main()
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
        using var window = new GlfwWindow(
            loggerFactory,
            1024,
            768,
            "ImGuiImplVulkan",
            resizable: true
        );
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

        // uint g_MinImageCount = 2;

        // Create Framebuffers
        // var (w, h) = window.GetFramebufferSize();
        // using var g_MainWindowData = new ImGui_ImplVulkanH_Window(
        //     vi,
        //     vd,
        //     vk_instance.Instance,
        //     vk_instance.PhysicalDevice,
        //     vk_instance.QueueFamily,
        //     vk_instance.Device,
        //     surface,
        //     w,
        //     h,
        //     g_MinImageCount
        // );

        // Setup Dear ImGui context
        using var imgui_context = new ImGuiContext();
        var io = ImGui.GetIO();

        // Setup scaling
        //     ImGuiStyle& style = ImGui.GetStyle();
        //     style.ScaleAllSizes(main_scale);        // Bake a fixed style scale. (until we have a solution for dynamic style scaling, changing this requires resetting Style + calling this again)
        //     style.FontScaleDpi = main_scale;        // Set initial font scale. (in docking branch: using io.ConfigDpiScaleFonts=true automatically overrides this for every window depending on the current monitor)

        // Setup Platform/Renderer backends
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

        // Load Fonts
        // - If fonts are not explicitly loaded, Dear ImGui will select an embedded font: either AddFontDefaultVector() or AddFontDefaultBitmap().
        //   This selection is based on (style.FontSizeBase * style.FontScaleMain * style.FontScaleDpi) reaching a small threshold.
        // - You can load multiple fonts and use ImGui.PushFont()/PopFont() to select them.
        // - If a file cannot be loaded, AddFont functions will return a nullptr. Please handle those errors in your code (e.g. use an assertion, display an error and quit).
        // - Read 'docs/FONTS.md' for more instructions and details.
        // - Use '#define IMGUI_ENABLE_FREETYPE' in your imconfig file to use FreeType for higher quality font rendering.
        // - Remember that in C/C++ if you want to include a backslash \ in a string literal you need to write a double backslash \\ !
        //style.FontSizeBase = 20.0f;
        //io.Fonts.AddFontDefaultVector();
        //io.Fonts.AddFontDefaultBitmap();
        //io.Fonts.AddFontFromFileTTF("c:\\Windows\\Fonts\\segoeui.ttf");
        //io.Fonts.AddFontFromFileTTF("../../misc/fonts/DroidSans.ttf");
        //io.Fonts.AddFontFromFileTTF("../../misc/fonts/Roboto-Medium.ttf");
        //io.Fonts.AddFontFromFileTTF("../../misc/fonts/Cousine-Regular.ttf");
        //ImFont* font = io.Fonts.AddFontFromFileTTF("c:\\Windows\\Fonts\\ArialUni.ttf");
        //IM_ASSERT(font != nullptr);

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

        // Our state
        bool show_demo_window = true;
        bool show_another_window = false;
        var clear_color = new Vector4(0.45f, 0.55f, 0.60f, 1.00f);
        float f = 0.0f;
        int counter = 0;

        // Main loop
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
            //         ImGui_ImplVulkan_NewFrame();
            implGlfw.NewFrame();
            ImGui.NewFrame();

            // 1. Show the big demo window (Most of the sample code is in ImGui.ShowDemoWindow()! You can browse its code to learn more about Dear ImGui!).
            if (show_demo_window)
                ImGui.ShowDemoWindow(ref show_demo_window);

            // 2. Show a simple window that we create ourselves. We use a Begin/End pair to create a named window.
            {
                ImGui.Begin("Hello, world!"); // Create a window called "Hello, world!" and append into it.

                ImGui.Text("This is some useful text."); // Display some text (you can use a format strings too)
                ImGui.Checkbox("Demo Window", ref show_demo_window); // Edit bools storing our window open/close state
                ImGui.Checkbox("Another Window", ref show_another_window);

                ImGui.SliderFloat("float", ref f, 0.0f, 1.0f); // Edit 1 float using a slider from 0.0f to 1.0f
                ImGui.ColorEdit4("clear color", ref clear_color); // Edit 3 floats representing a color

                if (ImGui.Button("Button")) // Buttons return true when clicked (most widgets return true when edited/activated)
                    counter++;
                ImGui.SameLine();
                ImGui.Text($"counter = {counter}");

                ImGui.Text(
                    $"Application average {1000.0f / io.Framerate:F3} ms/frame ({io.Framerate:F1} FPS)"
                );
                ImGui.End();
            }

            // 3. Show another simple window.
            if (show_another_window)
            {
                ImGui.Begin("Another Window", ref show_another_window); // Pass a pointer to our bool variable (the window will have a closing button that will clear the bool when clicked)
                ImGui.Text("Hello from another window!");
                if (ImGui.Button("Close Me"))
                    show_another_window = false;
                ImGui.End();
            }

            // Rendering
            ImGui.Render();
            var draw_data = ImGui.GetDrawData();
            bool is_minimized = (
                draw_data.DisplaySize.X <= 0.0f || draw_data.DisplaySize.Y <= 0.0f
            );
            if (!is_minimized)
            {
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

                VkClearValue clearColor = default;
                clearColor.color.float32[0] = clear_color.X * clear_color.W;
                clearColor.color.float32[1] = clear_color.Y * clear_color.W;
                clearColor.color.float32[2] = clear_color.Z * clear_color.W;
                clearColor.color.float32[3] = clear_color.W;
                // if (
                //     g_MainWindowData.BeginRender(clear) is
                //     (
                //         uint frameIndex,
                //         VkSemaphore image_acquired_semaphore,
                //         VkSemaphore render_complete_semaphore,
                //         VkCommandBuffer commandBuffer
                //     )
                // )
                var commandBuffer = renderTarget.BeginRendering(imageIndex, [clearColor]);

                {
                    implVulkan.RenderImDrawData(
                        picked,
                        draw_data,
                        commandBuffer,
                        imageIndex,
                        new((uint)fb_width, (uint)fb_height)
                    );
                }
                renderTarget.EndRendering(imageIndex);
                renderTarget.EndSubmitCommandBuffer(
                    imageIndex,
                    [imageAvailableSemaphore],
                    renderFinishedSemaphore,
                    inFlightFence
                );

                if (!swapchain.Present(imageIndex, renderFinishedSemaphore))
                {
                    resized = true;
                }
            }
        }

        // Cleanup
        device.Api.vkDeviceWaitIdle().ThrowIfError();

        return 0;
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
