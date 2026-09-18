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

    public static JsonNode? GetProperty(this JsonNode? self, string key)
    {
        if (self is JsonObject o)
        {
            if (o.TryGetPropertyValue(key, out var value))
            {
                return value;
            }
        }
        return default;
    }

    public static JsonObject? GetObject(this JsonNode? self, string key)
    {
        return self.GetProperty(key)?.AsObject();
    }

    public static JsonArray? GetArray(this JsonNode? self, string key)
    {
        return self.GetProperty(key)?.AsArray();
    }
}
