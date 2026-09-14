namespace VrmImpl.Drawlist;

public delegate void MaterialAttributesFunc(IShader shader);

public record class Material(
    Shader Shader,
    MaterialAttributesFunc? SetAttribues = default,
    TextureImage? Image = default
) { }
