using System.Text.Json;
using System.Text.Json.Serialization;

namespace SocialApp.WebApi.Data._Shared;

public static class DocumentSerialization
{
    static readonly IDictionary<string, Type> _types = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);
    static DocumentSerialization()
    {
        _types = AppDomain
            .CurrentDomain
            .GetAssemblies()
            .SelectMany(a => a.GetTypes())
            .Where(t => t.IsAssignableTo(typeof(Document)))
            .ToDictionary(x => x.Name, x => x);
    }
    
    public static object? Deserialize(Type type, JsonElement json)
        => json.Deserialize(type, Options);
    
    public static readonly JsonSerializerOptions Options
        = new()
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault,
            WriteIndented = true,
            PropertyNameCaseInsensitive = true, 
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };

    public static Document? DeserializeDocument(JsonElement item)
    {
        var typeKey = item.GetProperty("type").GetString();
        if(string.IsNullOrWhiteSpace(typeKey))
            return null;
        
        if (_types.TryGetValue(typeKey, out var type))
        {
            return Deserialize(type, item) as Document;
        }

        return null;
    }

    public static Document? DeserializeDocument(Stream stream)
    {
        using var document = JsonDocument.Parse(stream);
        return DeserializeDocument(document.RootElement);
    }
}