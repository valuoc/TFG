using SocialApp.WebApi.Data._Shared;

namespace SocialApp.WebApi.Data.User;

public record PostCountsDocument(string UserId, string PostId, int LikeCount, int CommentCount, int ViewCount, string? CommentUserId, string? CommentPostId) 
    : Document(Key(UserId, PostId))
{
    public static DocumentKey Key(string userId, string postId)
    {
        var pk = "user:"+userId;
        var id = $"post:{postId}:post_counts";
        return new DocumentKey(pk, id);
    }
}