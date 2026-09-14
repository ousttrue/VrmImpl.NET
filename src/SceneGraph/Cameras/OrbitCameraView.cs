using System.Numerics;

namespace VrmImpl.SceneGraph;

/// <summary>
/// OrbitView
///
/// right drag vertical: pitch
/// right drag horizontal: yaw
/// wheel: dolly
/// middle drag: screen shift
/// </summary>
public class OrbitCamera() : ICameraView
{
    public Vector3 Shift = new(0, -1.2f, -3f);
    public float YawRadians = 0;
    public float PitchRadians = 0;

    public Matrix4x4 GetViewMatrix()
    {
        return Matrix4x4.Multiply(
            Matrix4x4.Multiply(
                Matrix4x4.CreateRotationY(YawRadians),
                Matrix4x4.CreateRotationX(PitchRadians)
            ),
            Matrix4x4.CreateTranslation(Shift)
        );
    }

    public void YawPitch(float xOffset, float yOffset)
    {
        const float speed = 0.02f;
        YawRadians += xOffset * speed;
        PitchRadians -= yOffset * speed;
        PitchRadians = Math.Clamp(PitchRadians, -MathF.PI / 2, MathF.PI / 2);
    }

    public void ScreenShift(float xOffset, float yOffset, float fovY, float viewportHeight)
    {
        float speed = MathF.Abs(Shift.Z) * MathF.Cos(fovY / 2);
        Shift.X += speed * xOffset / viewportHeight;
        Shift.Y += speed * yOffset / viewportHeight;
    }

    public void Dolly(float y)
    {
        if (y > 0)
        {
            Shift.Z *= 0.9f;
        }
        else if (y < 0)
        {
            Shift.Z *= 1.1f;
        }
    }
}
