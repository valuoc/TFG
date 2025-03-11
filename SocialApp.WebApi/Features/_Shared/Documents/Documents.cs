using System.Text.Json.Serialization;

namespace SocialApp.WebApi.Features.Documents;

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

public record PostDocument(string PostId, string UserId, DateTime CreationDate, DateTime LastUpdate, string Content, int LikeCount, int CommentCount) 
    : Document(Key(UserId, PostId))
{
    public static DocumentKey Key(string userId, string postId)
    {
        var pk = $"account:{userId}";
        var id = $"post:{postId}@";
        return new DocumentKey(pk, id);
    }
}

public record CommentDocument(string PostId, string CommentId, string UserId, DateTime CreationDate, DateTime LastUpdate, string Content, int LikeCount) 
    : Document(Key(UserId, PostId, CommentId))
{
    public static DocumentKey Key(string userId, string postId, string commentId)
    {
        var pk = $"account:{userId}";
        var id = $"post:{postId}:comment:{commentId}";
        return new DocumentKey(id, pk);
    }
}
