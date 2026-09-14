using System.Numerics;

namespace VrmImpl.Drawlist;

public interface IShader
{
    void SetUniform(string name, Vector3 value);
}
