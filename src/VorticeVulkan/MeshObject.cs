using VrmImpl.Drawlist;
using Vortice.Vulkan;

namespace VrmImpl.VorticeVulkan;

public class MeshObject(ArrayBufferObject Vertex, ArrayBufferObject? Index) : IDisposable
{
    public readonly ArrayBufferObject VertexBuffer = Vertex;
    public readonly ArrayBufferObject? IndexBuffer = Index;

    public void Dispose()
    {
        if (IndexBuffer is not null)
        {
            IndexBuffer.Dispose();
        }
        VertexBuffer.Dispose();
    }

    public static MeshObject Create(
        VkInstanceApi vki,
        VkDeviceApi vkd,
        VkPhysicalDevice physicalDevice,
        uint graphicsQueueFamilyIndex,
        VertexInfo vertices,
        IndexInfo? _indices
    )
    {
        return new(
            ArrayBufferObject.Create(
                vki,
                vkd,
                VkBufferUsageFlags.TransferDst | VkBufferUsageFlags.VertexBuffer,
                physicalDevice,
                graphicsQueueFamilyIndex,
                vertices.Layout.First().Stride,
                vertices.Data
            ),
            (_indices is IndexInfo indices)
                ? ArrayBufferObject.Create(
                    vki,
                    vkd,
                    VkBufferUsageFlags.TransferDst | VkBufferUsageFlags.IndexBuffer,
                    physicalDevice,
                    graphicsQueueFamilyIndex,
                    indices.IndexUnit,
                    indices.Data
                )
                : default
        );
    }
}
