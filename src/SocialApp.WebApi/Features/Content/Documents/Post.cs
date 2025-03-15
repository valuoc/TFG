using SocialApp.WebApi.Features.Documents;

namespace SocialApp.WebApi.Features.Content.Documents;

public record PostDocument(string UserId, string PostId, string Content, DateTime LastModify, string? CommentUserId, string? CommentPostId) 
    : Document(Key(UserId, PostId))
{
    public static DocumentKey Key(string userId, string postId)
    {
        var pk = "user:"+userId;
        var id = $"post:{postId}:post";
        return new DocumentKey(pk, id);
    }
    
    public static DocumentKey KeyPostsEnd(string userId)
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

public record PostCountsDocument(string UserId, string PostId, int LikeCount, int CommentCount, int ViewCount, DateTime LastModify, string? CommentUserId, string? CommentPostId) 
    : Document(Key(UserId, PostId))
{
    public static DocumentKey Key(string userId, string postId)
    {
        var pk = "user:"+userId;
        var id = $"post:{postId}:post_counts";
        return new DocumentKey(pk, id);
    }
}

public record CommentDocument(string UserId, string PostId, string ParentUserId, string ParentPostId, string Content, DateTime LastModify) 
    : Document(Key(ParentUserId, ParentPostId, PostId))
{
    public static DocumentKey Key(string parentUserId, string parentPostId, string commentId)
    {
        var pk = "user:"+parentUserId;
        var id = $"post:{parentPostId}:comment:{commentId}";
        return new DocumentKey(pk, id);
    }
}

public record CommentCountsDocument(string UserId, string PostId, string ParentUserId, string ParentPostId, int LikeCount, int CommentCount, int ViewCount, DateTime LastModify) 
    : Document(Key(ParentUserId, ParentPostId, PostId))
{
    public static DocumentKey Key(string parentUserId, string parentPostId, string commentId)
    {
        var pk = "user:"+parentUserId;
        var id = $"post:{parentPostId}:comment:{commentId}:counts";
        return new DocumentKey(pk, id);
    }
}