using SocialApp.WebApi.Data._Shared;

namespace SocialApp.WebApi.Data.User;

public record FeedPostDocument(string UserId, string PostUserId, string PostId, string Content, DateTime LastModify, int Version) 
    : Document(Key(UserId, PostUserId, PostId))
{
    public bool IsFeed => true;

    public static FeedPostDocument From(string feedUserId, PostDocument post)
        => new(feedUserId, post.UserId, post.PostId, post.Content, post.LastModify, post.Version);

    public static DocumentKey Key(string userId, string postUserId, string postId)
    {
        var pk = "user:"+userId;
        var id = $"feed:{postId}:{postUserId}:post";
        return new DocumentKey(pk, id);
    }
    
    public static DocumentKey KeyUserPostsEnd(string userId)
    {
        var pk = "user:"+userId;
        var id = "feed:z";
        return new DocumentKey(pk, id);
    }
    
    public static DocumentKey KeyPostItemsStart(string userId, string postId)
    {
        var pk = "user:"+userId;
        var id = $"feed:{postId}";
        return new DocumentKey(pk, id);
    }

    public static DocumentKey KeyPostItemsEnd(string userId, string postId)
    {
        var pk = "user:"+userId;
        var id = $"feed:{postId}:z"; // z as limit
        return new DocumentKey(pk, id);
    }
}