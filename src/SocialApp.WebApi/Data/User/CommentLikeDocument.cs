using SocialApp.WebApi.Data._Shared;

namespace SocialApp.WebApi.Data.User;

public record CommentLikeDocument(string ThreadUserId, string ThreadId, string CommentId, string UserId, bool Like) 
    : Document(Key(ThreadUserId, ThreadId, CommentId, UserId))
{
    public static DocumentKey Key(string threadUserId, string threadId, string commentId, string userId)
    {
        var pk = "user:"+threadUserId;
        var id = $"like:{threadId}:comment:{commentId}:user:{userId}";
        return new DocumentKey(pk, id);
    }
}