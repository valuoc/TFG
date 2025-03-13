using SocialApp.WebApi.Features.Documents;

namespace SocialApp.WebApi.Features.Content.Documents;

public record PostDocument(string UserId, string PostId, string Content, string CommentUserId, string CommentPostId) 
    : Document(Key(UserId, PostId))
{
    public static DocumentKey Key(string userId, string postId)
    {
        var pk = "user:"+userId;
        var id = "post:"+postId;
        return new DocumentKey(pk, id);
    }
}

public record PostCountsDocument(string UserId, string PostId, int Likes, int Comments, int Views) 
    : Document(Key(UserId, PostId))
{
    public static DocumentKey Key(string userId, string postId)
    {
        var pk = "user:"+userId;
        var id = $"post:{postId}:counts";
        return new DocumentKey(pk, id);
    }
}

public record CommentDocument(string UserId, string PostId, string CommentId, string ParentUserId, string ParentPostId, string Content) 
    : Document(Key(ParentUserId, ParentPostId, CommentId))
{
    public static DocumentKey Key(string parentUserId, string parentPostId, string commentId)
    {
        var pk = "user:"+parentUserId;
        var id = $"post:{parentPostId}:comment:{commentId}";
        return new DocumentKey(pk, id);
    }
}