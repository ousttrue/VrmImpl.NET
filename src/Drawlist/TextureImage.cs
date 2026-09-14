namespace VrmImpl.Drawlist;

public enum TextureImageType
{
    Png,
    Jpg,
}

public record class TextureImage(TextureImageType ImageType, byte[] Bytes) { }
