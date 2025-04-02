using SocialApp.WebApi.Data._Shared;

namespace SocialApp.WebApi.Data.User;

public record CommentCountsDocument(string ThreadUserId, string ThreadId, string UserId, string CommentId, int LikeCount, int CommentCount, int ViewCount) 
    : Document(Key(ThreadUserId, ThreadId, CommentId))
{
    public static DocumentKey Key(string threadUserId, string threadId, string commentId)
    {
        var pk = "user:"+threadUserId;
        var id = $"thread:{threadId}:comment:{commentId}:counts";
        return new DocumentKey(pk, id);
    }
}