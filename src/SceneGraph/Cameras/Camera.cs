using System.Numerics;

namespace VrmImpl.SceneGraph;

public class Camera(ICameraView view, CameraProjection projection)
{
    public ICameraView View = view;
    public CameraProjection Projection = projection;

    public Vector2 LastMousePosition;
    public bool MouseLeftDown;
    public bool MouseRightDown;
    public bool MouseMiddleDown;

    public Matrix4x4 ViewMatrix => View.GetViewMatrix();

    public Matrix4x4 ProjectionMatrix => Projection.GetPerspectiveProjectionMatrix();
}
