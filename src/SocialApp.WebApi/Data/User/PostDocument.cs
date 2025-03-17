using SocialApp.WebApi.Data._Shared;

namespace SocialApp.WebApi.Data.User;

public record PostDocument(string UserId, string PostId, string Content, DateTime LastModify, int Version, string? CommentUserId, string? CommentPostId) 
    : Document(Key(UserId, PostId))
{
    public static DocumentKey Key(string userId, string postId)
    {
        var pk = "user:"+userId;
        var id = $"post:{postId}:post";
        return new DocumentKey(pk, id);
    }
    
    public static DocumentKey KeyUserPostsEnd(string userId)
    {
        var pk = "user:"+userId;
        var id = "post:z";
        return new DocumentKey(pk, id);
    }
    
    public static DocumentKey KeyPostItemsStart(string userId, string postId)
    {
        var pk = "user:"+userId;
        var id = $"post:{postId}";
        return new DocumentKey(pk, id);
    }

    public static DocumentKey KeyPostItemsEnd(string userId, string postId)
    {
        var pk = "user:"+userId;
        var id = $"post:{postId}:z"; // z as limit
        return new DocumentKey(pk, id);
    }
}