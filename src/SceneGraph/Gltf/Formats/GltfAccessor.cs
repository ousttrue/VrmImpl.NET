namespace VrmImpl.SceneGraph.Gltf;

public class GltfAccessor
{
    public enum ComponentType
    {
        SBYTE = 5120,
        UBYTE = 5121,
        SSHORT = 5122,
        USHORT = 5123,
        UINT = 5125,
        FLOAT = 5126,
    }

    public int bufferView { get; set; } = -1;
    public required ComponentType componentType { get; set; }
    public required string type { get; set; }
    public required int count { get; set; }
    public int byteOffset { get; set; }

    public int GetElementSize()
    {
        switch (type)
        {
            case "SCALAR":
                switch (componentType)
                {
                    case ComponentType.USHORT:
                        return 1 * 2;

                    case ComponentType.UINT:
                    case ComponentType.FLOAT:
                        return 1 * 4;

                    default:
                        throw new NotImplementedException($"{componentType}");
                }

            case "VEC2":
                switch (componentType)
                {
                    case ComponentType.FLOAT:
                        return 2 * 4;

                    default:
                        throw new NotImplementedException($"{componentType}");
                }

            case "VEC3":
                switch (componentType)
                {
                    case ComponentType.FLOAT:
                        return 3 * 4;

                    default:
                        throw new NotImplementedException($"{componentType}");
                }

            case "VEC4":
                switch (componentType)
                {
                    case ComponentType.FLOAT:
                        return 4 * 4;

                    case ComponentType.USHORT:
                        return 2 * 4;

                    case ComponentType.UBYTE:
                        return 1 * 4;

                    default:
                        throw new NotImplementedException($"{componentType}");
                }

            case "MAT4":
                switch (componentType)
                {
                    case ComponentType.FLOAT:
                        return 16 * 4;

                    default:
                        throw new NotImplementedException($"{componentType}");
                }

            default:
                throw new NotImplementedException(type);
        }
    }
}
