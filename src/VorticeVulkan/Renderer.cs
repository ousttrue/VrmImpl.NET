using System.Runtime.InteropServices;
using VrmImpl.Drawlist;
using StbImageSharp;
using Vortice.Vulkan;

namespace VrmImpl.VorticeVulkan;

public class Renderer : IDisposable
{
    private readonly VkInstanceApi _vki;
    private readonly VkDeviceApi _vkd;
    private readonly VkPhysicalDevice _physicalDevice;
    private readonly uint _graphicsQueueFamilyIndex;
    private readonly uint _maxFlightCount;
    private readonly VkFormat _colorFormat;
    private readonly VkFormat _depthFormat;

    private readonly UniformBufferObject<WorldInfo>[] _uniformBuffers;
    private readonly TextureObject _whiteTexture;

    private readonly Dictionary<TextureImage, TextureObject> _textureMap = new();

    // mesh animation がある場合は swapchain 多重化する
    private readonly Dictionary<Mesh, MeshObject[]> _meshMap = new();

    // swapchain 多重化された staging buffer
    private readonly Dictionary<Mesh, ArrayBufferObject[]> _stagingMap = new();

    private readonly Dictionary<Shader, VulkanPipeline<ModelInfo>> _pipelineMap = new();

    public Renderer(
        VkInstanceApi vki,
        VkDeviceApi vkd,
        VkPhysicalDevice physicalDevice,
        uint graphicsQueueFamilyIndex,
        uint maxFlightCount,
        VkFormat colorFormat,
        VkFormat depthFormat
    )
    {
        _vki = vki;
        _vkd = vkd;
        _physicalDevice = physicalDevice;
        _graphicsQueueFamilyIndex = graphicsQueueFamilyIndex;
        _maxFlightCount = maxFlightCount;
        _colorFormat = colorFormat;
        _depthFormat = depthFormat;

        _uniformBuffers = new UniformBufferObject<WorldInfo>[maxFlightCount];
        for (int i = 0; i < maxFlightCount; i++)
        {
            _uniformBuffers[i] = new(vki, _vkd, physicalDevice);
        }

        _whiteTexture = new TextureObject(
            vki,
            vkd,
            physicalDevice,
            2,
            2,
            graphicsQueueFamilyIndex,
            Enumerable.Range(0, 2 * 2 * 4).Select(_ => (byte)255).ToArray()
        );
    }

    public void Dispose()
    {
        foreach (var ubo in _uniformBuffers)
        {
            ubo.Dispose();
        }
        _whiteTexture.Dispose();
        foreach (var (k, v) in _textureMap)
        {
            v.Dispose();
        }
        foreach (var (k, v) in _meshMap)
        {
            foreach (var item in v)
            {
                item.Dispose();
            }
        }
        foreach (var (k, v) in _stagingMap)
        {
            foreach (var item in v)
            {
                item.Dispose();
            }
        }
        foreach (var (k, v) in _pipelineMap)
        {
            v.Dispose();
        }
    }

    private unsafe void UpdateDescriptor(
        VkDescriptorSet desc,
        UniformBufferObject<WorldInfo> ubo,
        TextureObject texture
    )
    {
        VkDescriptorImageInfo imageInfo = new()
        {
            imageLayout = VkImageLayout.ShaderReadOnlyOptimal,
            imageView = texture.ImageView,
            sampler = texture.Sampler,
        };

        VkDescriptorBufferInfo bufferInfo = new()
        {
            buffer = ubo.Buffer,
            offset = 0,
            range = (ulong)Marshal.SizeOf<WorldInfo>(),
        };

        var writes = stackalloc[] {
            new VkWriteDescriptorSet()
            {
                sType = VkStructureType.WriteDescriptorSet,
                dstSet = desc,
                dstBinding = 0,
                dstArrayElement = 0,
                descriptorType = VkDescriptorType.UniformBuffer,
                descriptorCount = 1,
                pBufferInfo = &bufferInfo,
            },
            new VkWriteDescriptorSet()
            {
                sType = VkStructureType.WriteDescriptorSet,
                dstSet = desc,
                dstBinding = 1,
                dstArrayElement = 0,
                descriptorType = VkDescriptorType.CombinedImageSampler,
                descriptorCount = 1,
                pImageInfo = &imageInfo,
            },
        };

        _vkd.vkUpdateDescriptorSets(2, writes, 0, null);
    }

    public void ApplyAnimation(
        IReadOnlyList<Draw> drawlist,
        uint imageIndex,
        VkCommandBuffer commandBuffer
    )
    {
        foreach (var (mesh, _, _animation) in drawlist)
        {
            var meshObject = GetOrCreate(mesh, imageIndex, _animation is not null);

            if (_animation is VertexInfo animation)
            {
                var staging = GetOrCreateStaging(mesh, imageIndex, animation);
                VkHelper.CopyBuffer(
                    _vkd,
                    commandBuffer,
                    staging.Buffer,
                    meshObject.VertexBuffer.Buffer,
                    staging.ByteLength
                );
            }
        }
    }

    public void Render(
        uint frameCount,
        Draw draw,
        uint imageIndex,
        VkCommandBuffer commandBuffer,
        VkExtent2D extent,
        WorldInfo world
    )
    {
        var (mesh, matrix, _animation) = draw;
        {
            var meshObject = GetOrCreate(mesh, imageIndex, _animation is not null);

            meshObject.VertexBuffer.Bind(commandBuffer);
            Action<VkCommandBuffer, uint, uint> Draw;
            if (meshObject.IndexBuffer is ArrayBufferObject indexBuffer)
            {
                indexBuffer.Bind(commandBuffer);
                Draw = indexBuffer.Draw;
            }
            else
            {
                Draw = meshObject.VertexBuffer.Draw;
            }

            var ubo = _uniformBuffers[imageIndex];
            ubo.Upload(world);
            foreach (var prim in mesh.Primitives)
            {
                var (shader, _, image) = prim.Material;
                var pipeline = GetOrCreate(shader);
                var descSet = pipeline.Bind(frameCount, commandBuffer, extent, imageIndex);

                pipeline.PushConstant(commandBuffer, new(matrix));

                if (image is TextureImage textureImage)
                {
                    var texture = GetOrCreate(textureImage);
                    UpdateDescriptor(descSet, ubo, texture);
                }
                else
                {
                    UpdateDescriptor(descSet, ubo, _whiteTexture);
                }

                Draw(commandBuffer, (uint)prim.DrawOffset, (uint)prim.DrawCount);
            }
        }
    }

    private MeshObject GetOrCreate(Mesh mesh, uint imageIndex, bool hasAnimation)
    {
        if (!hasAnimation)
        {
            imageIndex = 0;
        }

        if (_meshMap.TryGetValue(mesh, out var meshObjects))
        {
            return meshObjects[imageIndex];
        }

        meshObjects = new MeshObject[_uniformBuffers.Length];
        for (int i = 0; i < meshObjects.Length; ++i)
        {
            meshObjects[i] = MeshObject.Create(
                _vki,
                _vkd,
                _physicalDevice,
                _graphicsQueueFamilyIndex,
                mesh.Vertices,
                mesh.Indices
            );
        }
        _meshMap.Add(mesh, meshObjects);
        return meshObjects[imageIndex];
    }

    private unsafe ArrayBufferObject GetOrCreateStaging(
        Mesh mesh,
        uint imageIndex,
        VertexInfo values
    )
    {
        if (_stagingMap.TryGetValue(mesh, out var stagings))
        {
            var staging = stagings[imageIndex];
            using (var map = staging.Map())
            {
                values.Data.CopyTo(new Span<byte>(map.ToPointer<byte>(), values.Data.Length));
            }
            return staging;
        }
        stagings = new ArrayBufferObject[_uniformBuffers.Length];
        for (int i = 0; i < stagings.Length; ++i)
        {
            stagings[i] = ArrayBufferObject.Create(
                _vki,
                _vkd,
                VkBufferUsageFlags.TransferSrc,
                _physicalDevice,
                values.Data
            );
        }
        _stagingMap.Add(mesh, stagings);
        return stagings[imageIndex];
    }

    private VulkanPipeline<ModelInfo> GetOrCreate(Shader shader)
    {
        if (_pipelineMap.TryGetValue(shader, out var pipeline))
        {
            return pipeline;
        }

        byte[] vsSpv;
        byte[] fsSpv;
        if (shader.Spv is (byte[] _vsSpv, byte[] _fsSpv))
        {
            vsSpv = _vsSpv;
            fsSpv = _fsSpv;
        }
        else
        {
            vsSpv = ShaderResource.VertexSpv;
            fsSpv = ShaderResource.FragmentSpv;
        }
        using var vs = new VulkanShaderModule(_vkd, vsSpv);
        using var fs = new VulkanShaderModule(_vkd, fsSpv);
        var (topology, layout) = shader.VertexInput;
        var (binding, attributes) = VkHelper.InputFromLayout(layout);
        pipeline = new(
            _vkd,
            vs,
            fs,
            ToVulkan(topology),
            binding,
            attributes,
            _maxFlightCount,
            ShaderResource.DescriptorSetLayoutBindings,
            _colorFormat,
            _depthFormat,
            ShaderResource.PipelineDepthStencilState
        );
        _pipelineMap.Add(shader, pipeline);
        return pipeline;
    }

    private static VkPrimitiveTopology ToVulkan(Topology t)
    {
        switch (t)
        {
            case Topology.Lines:
                return VkPrimitiveTopology.LineList;
            case Topology.Triangles:
                return VkPrimitiveTopology.TriangleList;
            default:
                throw new NotImplementedException();
        }
    }

    private TextureObject GetOrCreate(TextureImage image)
    {
        if (_textureMap.TryGetValue(image, out var texture))
        {
            return texture;
        }
        // png to image
        var img = ImageResult.FromStream(
            new MemoryStream(image.Bytes),
            ColorComponents.RedGreenBlueAlpha
        );
        texture = new TextureObject(
            _vki,
            _vkd,
            _physicalDevice,
            (uint)img.Width,
            (uint)img.Height,
            _graphicsQueueFamilyIndex,
            img.Data
        );
        _textureMap.Add(image, texture);
        return texture;
    }
}
