using System.Numerics;

namespace VrmImpl.SceneGraph;

public interface ICameraView
{
    Matrix4x4 GetViewMatrix();
}
