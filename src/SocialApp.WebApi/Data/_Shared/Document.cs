using System.Text.Json.Serialization;

namespace SocialApp.WebApi.Data._Shared;

public readonly record struct DocumentKey(string Pk, string Id);

public abstract record Document
{
    public string Id { get; private set; }
    public string Pk { get; private set; }
    public string Type { get; private set; }
    
    protected Document(DocumentKey key)
    {
        Id = key.Id;
        Pk = key.Pk;
        Type = GetType().Name;
    }

    [JsonIgnore]
    public string? ETag { get; set; }
    
    public int Ttl { get; set; } = -1;
}
