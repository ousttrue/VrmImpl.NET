using System.Numerics;

namespace VrmImpl.SceneGraph;

/// <summary>
/// FirstPersonView
/// [W][A][S][D] key + Mouse
/// </summary>
public class FpvCamera(Vector3 position, Vector3 front, Vector3 up) : ICameraView
{
    public Vector3 Position { get; set; } = position;
    public Vector3 Front { get; set; } = front;

    public Vector3 Up { get; private set; } = up;

    public float YawDegrees { get; set; } = -90f;
    public float PitchDegrees { get; set; }

    public void MoveFront(float moveSpeed)
    {
        Position += Front * moveSpeed;
    }

    public void MoveRight(float moveSpeed)
    {
        Position += Vector3.Normalize(Vector3.Cross(Front, Up)) * moveSpeed;
    }

    public void ModifyDirection(float xOffset, float yOffset)
    {
        YawDegrees += xOffset;
        PitchDegrees -= yOffset;

        //We don't want to be able to look behind us by going over our head or under our feet so make sure it stays within these bounds
        PitchDegrees = Math.Clamp(PitchDegrees, -89f, 89f);

        var cameraDirection = Vector3.Zero;
        cameraDirection.X =
            MathF.Cos(MathF.PI * (YawDegrees / 180f)) * MathF.Cos(MathF.PI * (PitchDegrees / 180f));
        cameraDirection.Y = MathF.Sin(MathF.PI * (PitchDegrees / 180f));
        cameraDirection.Z =
            MathF.Sin(MathF.PI * (YawDegrees / 180f)) * MathF.Cos(MathF.PI * (PitchDegrees / 180f));

        Front = Vector3.Normalize(cameraDirection);
    }

    public Matrix4x4 GetViewMatrix()
    {
        return Matrix4x4.CreateLookAt(Position, Position + Front, Up);
    }
}
