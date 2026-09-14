using System.Text.Json.Nodes;

namespace VrmImpl.SceneGraph;

static class JsonNodeExtensions
{
    public static T GetPropertyValue<T>(this JsonNode? self, string key)
    {
        if (self is null)
        {
            throw new InvalidDataException("self is null");
        }
        if (!self.AsObject().TryGetPropertyValue(key, out JsonNode? val))
        {
            throw new InvalidDataException($"no '{key}' property");
        }
        if (val is null)
        {
            throw new InvalidDataException($".{key} is null");
        }
        return val.GetValue<T>();
    }

    public static T GetPropertyValueOrDefault<T>(this JsonNode? self, string key, T defaultValue)
    {
        try
        {
            return self.GetPropertyValue<T>(key);
        }
        catch (InvalidDataException)
        {
            return defaultValue;
        }
    }
}
