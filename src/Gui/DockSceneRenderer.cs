using System.Numerics;
using ImGuiNET;
using VrmImpl.SceneGraph;
using Vortice.Vulkan;
using VrmImpl.SceneRenderer;

namespace VrmImpl.Gui;

public class DockSceneRenderer : IDisposable
{
    private readonly VkInstanceApi _vki;
    private readonly VkDeviceApi _vkd;
    private readonly VkPhysicalDevice _physicalDevice;
    private readonly uint _graphicsQueueFamilyIndex;
    private readonly uint _imageCount;
    private readonly SceneTexture.IPipeline _pipeline;

    private readonly Dictionary<Scene, (SceneTexture, Dock)> _sceneMap = [];

    private readonly DockManager _dockManager = new();
    private readonly List<VkSemaphore> _renderTargetEnds = [];

    public DockSceneRenderer(
        VkInstanceApi vki,
        VkDeviceApi vkd,
        VkPhysicalDevice physicalDevice,
        uint graphicsQueueFamilyIndex,
        uint imageCount,
        SceneTexture.IPipeline igPipeline
    )
    {
        _vki = vki;
        _vkd = vkd;
        _physicalDevice = physicalDevice;
        _graphicsQueueFamilyIndex = graphicsQueueFamilyIndex;
        _imageCount = imageCount;
        _pipeline = igPipeline;

        _dockManager.AddDock("Dear ImGui Demo", "d", ImGui.ShowDemoWindow);
    }

    public void Dispose()
    {
        foreach (var (scene, (sceneTexture, dock)) in _sceneMap)
        {
            sceneTexture.Dispose();
        }
    }

    /// <summary>
    /// 1st. ImGui.Begin and get ImGui.GetContentRegionAvail() as renderTargetSize.
    /// 2nd. Render Scene to texture that has renderTargetSize.
    /// 3rd. ImGui.Image use renderTarget.
    ///
    /// return renderFinishedSemaphore for renderTarget.
    /// </summary>
    private static VkSemaphore? ImGuiRenderTarget(SceneTexture sceneTexture)
    {
        VkSemaphore? _semaphore = default;
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
        ImGui.SetNextWindowSize(new(256, 256), ImGuiCond.FirstUseEver);
        if (
            ImGui.Begin(
                sceneTexture.Name,
                ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse
            )
        )
        {
            var wh = ImGui.GetContentRegionAvail();
            var xy = ImGui.GetWindowPos();
            xy.Y += ImGui.GetFrameHeight();
            var io = ImGui.GetIO();

            if (ImGui.IsWindowHovered(ImGuiHoveredFlags.RootAndChildWindows))
            {
                if (io.MouseWheel != 0)
                    sceneTexture.CameraView.Dolly(io.MouseWheel);
                if (ImGui.IsMouseDown(ImGuiMouseButton.Right) && io.MouseDownDuration[1] == 0.0f)
                    ImGui.SetWindowFocus();
                if (ImGui.IsMouseDown(ImGuiMouseButton.Middle) && io.MouseDownDuration[2] == 0.0f)
                    ImGui.SetWindowFocus();
            }

            if (ImGui.IsWindowFocused(ImGuiFocusedFlags.RootAndChildWindows))
            {
                if (ImGui.IsMouseDragging(ImGuiMouseButton.Right))
                    sceneTexture.CameraView.YawPitch(io.MouseDelta.X, -io.MouseDelta.Y);

                if (ImGui.IsMouseDragging(ImGuiMouseButton.Middle))
                    sceneTexture.CameraView.ScreenShift(
                        io.MouseDelta.X,
                        -io.MouseDelta.Y,
                        sceneTexture.CameraProjection.FovY,
                        sceneTexture.CameraProjection.ViewportSize.Y
                    );
            }

            if (
                sceneTexture.Render(new((uint)wh.X, (uint)wh.Y)) is
                (VkSemaphore semaphore, VkDescriptorSet texture)
            )
            {
                _semaphore = semaphore;
                // ImGui.ImageButton(
                //     ctypes.c_void_p(texture), (w, h), (0.0, 0.0), (1.0, 1.0), 0, bg_col=ImGui.ImVec4(0, 0, 0, 1), tint_col=ImGui.ImVec4(1, 1, 1, 1))
                // https://gamedev.stackexchange.com/questions/140693/how-can-i-render-an-opengl-scene-into-an-imgui-window
                // Using a Child allow to fill all the space of the window.
                // It also alows customization
                ImGui.BeginChild("cameraview");
                // Because I use the texture from OpenGL, I need to invert the V from the UV.
                ImGui.Image((IntPtr)texture.Handle, wh, new(0, 0), new(1, 1));
                ImGui.EndChild();
            }
        }
        ImGui.End();
        ImGui.PopStyleVar();

        return _semaphore;
    }

    private (SceneTexture, Dock) GetOrCreateRenderTextureAndDock(Scene scene)
    {
        if (_sceneMap.TryGetValue(scene, out var scene_dock))
        {
            return scene_dock;
        }

        var sceneTexture = new SceneTexture(
            scene.Asset,
            _vki,
            _vkd,
            _physicalDevice,
            _graphicsQueueFamilyIndex,
            _imageCount,
            scene,
            _pipeline
        );

        var dock = new Dock(
            scene.Asset,
            "",
            (ref bool p_open) =>
            {
                if (ImGuiRenderTarget(sceneTexture) is VkSemaphore semaphore)
                {
                    _renderTargetEnds.Add(semaphore);
                }
            }
        );

        _dockManager.AddDock(dock);
        _sceneMap.Add(scene, (sceneTexture, dock));

        return (sceneTexture, dock);
    }

    public IReadOnlyList<VkSemaphore> RenderSceneTextures(
        IReadOnlyList<Scene> scenes,
        float deltaTime,
        uint imageIndex
    )
    {
        _renderTargetEnds.Clear();
        foreach (var scene in scenes)
        {
            var (sceneTexture, _) = GetOrCreateRenderTextureAndDock(scene);
            sceneTexture.SetFrameInfo(new(deltaTime, imageIndex));
        }
        _dockManager.Draw();
        return _renderTargetEnds;
    }
}
