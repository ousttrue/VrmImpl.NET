using System.Reflection.Metadata;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Silk.NET.Core.Native;
using Silk.NET.GLFW;

namespace VrmImpl.Gui;

public unsafe class GlfwWindow : IDisposable
{
    static readonly Glfw glfw;
    public static readonly string[] InstanceExtensions;

    static GlfwWindow()
    {
        glfw = GlfwProvider.GLFW.Value ?? throw new NullReferenceException();
        var extensions = glfw.GetRequiredInstanceExtensions(out var extensions_count);
        InstanceExtensions = new string[extensions_count];
        for (int i = 0; i < extensions_count; ++i)
        {
            InstanceExtensions[i] =
                Marshal.PtrToStringAnsi((nint)extensions[i]) ?? throw new Exception();
        }
    }

    const uint WIDTH = 800;
    const uint HEIGHT = 600;

    private readonly ILogger _logger;
    private readonly WindowHandle* _window;
    public WindowHandle* WindowHandle => _window;

    public GlfwWindow(ILoggerFactory factory, bool resizable)
    {
        _logger = factory.CreateLogger("Glfw");
        glfw.Init();

        glfw.WindowHint(WindowHintClientApi.ClientApi, ClientApi.NoApi);
        glfw.WindowHint(WindowHintBool.Resizable, resizable);

        _window = glfw.CreateWindow((int)WIDTH, (int)HEIGHT, "Vulkan", null, null);

        // var m = glfw.GetWindowMonitor(_window);
        var m = glfw.GetPrimaryMonitor();
        glfw.GetMonitorContentScale(m, out var xscale, out var yscale);
        _logger.LogDebug($"MonitorScale: {xscale}:{yscale}");
    }

    public void Dispose()
    {
        glfw.DestroyWindow(_window);

        glfw.Terminate();
    }

    public ReadOnlySpan<IntPtr> GetVkExtensions()
    {
        var glfwExtensions = glfw.GetRequiredInstanceExtensions(out var glfwExtensionCount);
        return new(glfwExtensions, (int)glfwExtensionCount);
    }

    const int VK_SUCCESS = 0;

    public ulong CreateVkSurface(nint vkInstance)
    {
        VkNonDispatchableHandle _surface;
        if (
            glfw.CreateWindowSurface(new VkHandle(vkInstance), _window, null, &_surface)
            != VK_SUCCESS
        )
        {
            throw new Exception("failed to create window surface!");
        }
        return _surface.Handle;
    }

    public (int, int) GetExtent()
    {
        glfw.GetFramebufferSize(_window, out var width, out var height);
        return (width, height);
    }

    public (int, int)? NewFrame()
    {
        if (glfw.WindowShouldClose(_window))
        {
            return default;
        }
        glfw.PollEvents();
        return GetExtent();
    }

    public bool IsIconified()
    {
        return glfw.GetWindowAttrib(_window, WindowAttributeGetter.Iconified);
    }
}
