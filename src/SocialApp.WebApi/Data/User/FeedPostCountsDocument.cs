using SocialApp.WebApi.Data._Shared;

namespace SocialApp.WebApi.Data.User;

public record FeedPostCountsDocument(string UserId, string PostUserId, string PostId, int LikeCount, int CommentCount, int ViewCount) 
    : Document(Key(UserId, PostUserId, PostId))
{
    public bool IsFeed => true;

    public static FeedPostCountsDocument From(string feedUserId, PostCountsDocument post)
        => new(feedUserId, post.UserId, post.PostId, post.LikeCount, post.CommentCount, post.ViewCount);

    public static DocumentKey Key(string userId, string postUserId, string postId)
    {
        var pk = "user:"+userId;
        var id = $"feed:{postId}:{postUserId}:post_counts";
        return new DocumentKey(pk, id);
    }
}