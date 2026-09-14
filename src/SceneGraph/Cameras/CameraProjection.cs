using System.Numerics;

namespace VrmImpl.SceneGraph;

public class CameraProjection
{
    Vector2 _frameSize = default;
    public Vector2 ViewportSize => _frameSize;
    float _aspectRatio = 1f;
    public float AspectRatio => _aspectRatio;
    private float fovY = MathF.PI * (45f / 180f);
    public float FovY
    {
        get => fovY;
        set { fovY = Math.Clamp(value, MathF.PI * (1f / 180f), MathF.PI * (45f / 180f)); }
    }
    public float ZNear = 0.1f;
    public float ZFar = 100f;

    public static CameraProjection FromFrameSize(Vector2 frameSize)
    {
        var p = new CameraProjection();
        p.SetFrameSize(frameSize);
        return p;
    }

    public void SetFrameSize(Vector2 frameSize)
    {
        _frameSize = frameSize;
        _aspectRatio = _frameSize.X / _frameSize.Y;
    }

    public Matrix4x4 GetPerspectiveProjectionMatrix()
    {
        var m = Matrix4x4.CreatePerspectiveFieldOfView(FovY, AspectRatio, ZNear, ZFar);
        m.M22 *= -1f;
        return m;
        // return MakeProjectionMatrix(fovY, AspectRatio, ZNear, ZFar);
    }

    // https://computergraphics.stackexchange.com/questions/12448/vulkan-perspective-matrix-vs-opengl-perspective-matrix
    static Matrix4x4 MakeProjectionMatrix(float fovy_rads, float s, float near, float far)
    {
        float g = 1.0f / MathF.Tan(fovy_rads * 0.5f);
        float k = far / (far - near);

        // csharpier-ignore
        return new(
            g / s, 0.0f, 0.0f, 0.0f,
            0.0f, g, 0.0f, 0.0f,
            0.0f, 0.0f, k, -near * k,
            0.0f, 0.0f, 1.0f, 0.0f
        );
    }
}
