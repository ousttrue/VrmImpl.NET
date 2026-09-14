using System.Numerics;
using System.Runtime.InteropServices;

namespace VrmImpl.Drawlist;

public record Skin(int[] Joints, Matrix4x4[] InversedBindMatrices)
{
    private byte[] _dst = [];

    public byte[] Deform<T>(
        Matrix4x4 baseMatrix,
        ReadOnlySpan<T> src,
        ReadOnlySpan<SkinVertex> skinVertices,
        IReadOnlyList<Matrix4x4> transformMatrices,
        Action<Span<T>, int> clearPosNormal,
        Action<Span<T>, ReadOnlySpan<T>, int, Matrix4x4, float> addTransformWeighted
    )
        where T : unmanaged
    {
        var dst = MemoryMarshal.Cast<byte, T>(_dst.AsSpan());
        if (dst.Length != src.Length)
        {
            // alloc
            _dst = new byte[src.Length * Marshal.SizeOf<T>()];
            dst = MemoryMarshal.Cast<byte, T>(_dst.AsSpan());
        }

        for (int i = 0; i < src.Length; ++i)
        {
            var skin = skinVertices[i];
            dst[i] = src[i];
            clearPosNormal(dst, i);
            if (skin.Weights.X > 0)
            {
                var m = GetMatrix((int)skin.Joints.X, transformMatrices, baseMatrix);
                addTransformWeighted(dst, src, i, m, skin.Weights.X);
            }
            if (skin.Weights.Y > 0)
            {
                var m = GetMatrix((int)skin.Joints.Y, transformMatrices, baseMatrix);
                addTransformWeighted(dst, src, i, m, skin.Weights.Y);
            }
            if (skin.Weights.Z > 0)
            {
                var m = GetMatrix((int)skin.Joints.Z, transformMatrices, baseMatrix);
                addTransformWeighted(dst, src, i, m, skin.Weights.Z);
            }
            if (skin.Weights.W > 0)
            {
                var m = GetMatrix((int)skin.Joints.W, transformMatrices, baseMatrix);
                addTransformWeighted(dst, src, i, m, skin.Weights.W);
            }
        }
        return _dst;
    }

    private Matrix4x4 GetMatrix(int jointIndex, IReadOnlyList<Matrix4x4> transformMatrices, Matrix4x4 baseMatrix)
    {
        var bindMatrix = InversedBindMatrices[jointIndex];
        var transformMatrix = transformMatrices[Joints[jointIndex]];
        return Matrix4x4.Multiply(
            Matrix4x4.Multiply(bindMatrix, transformMatrix),
            baseMatrix
        );
    }
}
