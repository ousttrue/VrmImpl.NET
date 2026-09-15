using System.Numerics;
using System.Runtime.InteropServices;
using Vortice.Vulkan;
using VrmImpl.Drawlist;
using VrmImpl.Drawlist.Gizmo;
using VrmImpl.SceneGraph;
using VrmImpl.VorticeVulkan;

namespace VrmImpl.SceneRenderer;

public class SceneTexture : IDisposable
{
    private static int _sceneCount = 0;

    // unique !
    public readonly string Name;

    private readonly LineRenderer _lineRenderer;

    private readonly VkInstanceApi _vki;
    private readonly VkDeviceApi _vkd;
    private readonly VkPhysicalDevice _physicalDevice;
    private readonly uint _graphicsQueueFamilyIndex;
    private readonly uint _flightCount;

    public interface IPipeline
    {
        VkDescriptorSet BindTexture(TextureObject texture);
        void UnbindTexture(VkDescriptorSet descriptorSet);
    }

    private readonly IPipeline _pipeline;

    private readonly VorticeVulkan.Renderer _renderer;

    public readonly Scene Scene;
    public readonly OrbitCamera CameraView = new();
    public readonly CameraProjection CameraProjection = new();

    private (RenderTextureObject, VkDescriptorSet[])? _renderTexture;
    private uint _frameCount = 0;

    // public record struct FrameInfo(double Delta, uint ImageIndex) { }

    // private FrameInfo _frameInfo = default;

    // public void SetFrameInfo(FrameInfo info)
    // {
    //     _frameInfo = info;
    // }

    public SceneTexture(
        string name,
        VkInstanceApi vki,
        VkDeviceApi vk,
        VkPhysicalDevice physicalDevice,
        uint graphicsQueueFamilyIndex,
        uint flightCount,
        Scene scene,
        IPipeline igPipeline
    )
    {
        Name = $"{name}##id{_sceneCount++}";
        _vki = vki;
        _vkd = vk;
        _physicalDevice = physicalDevice;
        _graphicsQueueFamilyIndex = graphicsQueueFamilyIndex;
        _flightCount = flightCount;
        _pipeline = igPipeline;
        Scene = scene;

        _renderer = new Renderer(
            vki,
            vk,
            physicalDevice,
            graphicsQueueFamilyIndex,
            flightCount,
            VkFormat.R8G8B8A8Unorm,
            VkFormat.D24UnormS8Uint
        );

        _lineRenderer = new LineRenderer(
            vki,
            vk,
            physicalDevice,
            graphicsQueueFamilyIndex,
            flightCount,
            VkFormat.R8G8B8A8Unorm,
            VkFormat.D24UnormS8Uint
        );
    }

    public void Dispose()
    {
        _lineRenderer.Dispose();
        _renderer.Dispose();
        if (_renderTexture is (RenderTextureObject renderTexture, VkDescriptorSet[] descs))
        {
            renderTexture.Dispose();
            foreach (var desc in descs)
            {
                _pipeline.UnbindTexture(desc);
            }
        }
    }

    public void Resize(VkExtent2D extent)
    {
        CameraProjection.SetFrameSize(new(extent.width, extent.height));

        if (_renderTexture is (RenderTextureObject renderTexture, VkDescriptorSet[] descs))
        {
            if (!renderTexture.RenderTarget.Extent.Equals(extent))
            {
                _vkd.vkDeviceWaitIdle();

                foreach (var desc in descs)
                {
                    _pipeline.UnbindTexture(desc);
                }
                renderTexture.Dispose();
                renderTexture = new RenderTextureObject(
                    _vki,
                    _vkd,
                    _physicalDevice,
                    _graphicsQueueFamilyIndex,
                    _flightCount,
                    extent
                );
                descs = renderTexture.Textures.Select(_pipeline.BindTexture).ToArray();
                _renderTexture = (renderTexture, descs);
            }
        }
        else
        {
            renderTexture = new RenderTextureObject(
                _vki,
                _vkd,
                _physicalDevice,
                _graphicsQueueFamilyIndex,
                _flightCount,
                extent
            );
            descs = renderTexture.Textures.Select(x => _pipeline.BindTexture(x)).ToArray();
            _renderTexture = (renderTexture, descs);
        }
    }

    public unsafe (VkSemaphore, VkDescriptorSet)? Render(uint imageIndex, VkExtent2D extent)
    {
        Resize(extent);

        if (_renderTexture is not (RenderTextureObject renderTexture, VkDescriptorSet[] descs))
        {
            return null;
        }

        var frameCount = _frameCount++;

        var drawlist = Scene.MakeDrawList();
        var world = new WorldInfo(
            CameraView.GetViewMatrix(),
            CameraProjection.GetPerspectiveProjectionMatrix()
        );

        var (commandBuffer, imageView, image, semaphore) = renderTexture.RenderTarget.BeginCommand(
            imageIndex
        );

        _renderer.ApplyAnimation(drawlist, imageIndex, commandBuffer);

        _lineRenderer.Clear();
        _lineRenderer.Push(MemoryMarshal.Cast<byte, LineVertex>(XZGrid.CreateMesh().Vertices.Data));
        _lineRenderer.ApplyAnimation(Scene.Nodes, imageIndex, commandBuffer);

        renderTexture.RenderTarget.BeginRendering(
            commandBuffer,
            imageView,
            image,
            VkAttachmentLoadOp.Clear,
            new(0.1f, 0.1f, 0.1f, 1f),
            VkAttachmentStoreOp.Store,
            VkAttachmentLoadOp.Clear,
            new(depth: 1, stencil: 0),
            VkAttachmentStoreOp.DontCare,
            VkImageLayout.ColorAttachmentOptimal
        );

        // render scene
        _lineRenderer.Render(
            frameCount,
            imageIndex,
            commandBuffer,
            renderTexture.RenderTarget.Extent,
            world
        );

        foreach (var draw in drawlist)
        {
            _renderer.Render(
                frameCount,
                draw,
                imageIndex,
                commandBuffer,
                renderTexture.RenderTarget.Extent,
                world
            );
        }

        renderTexture.RenderTarget.EndRenderingAndCommandBuffer(
            commandBuffer,
            [],
            default,
            semaphore,
            image,
            VkImageLayout.ShaderReadOnlyOptimal
        );

        return (semaphore, descs[imageIndex]);
    }
}
