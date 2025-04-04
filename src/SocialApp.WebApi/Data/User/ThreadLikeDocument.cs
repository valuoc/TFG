using SocialApp.WebApi.Data._Shared;

namespace SocialApp.WebApi.Data.User;

public record ThreadLikeDocument(string ThreadUserId, string ThreadId, string UserId, bool Like) 
    : Document(Key(ThreadUserId, ThreadId, UserId))
{
    public static DocumentKey Key(string threadUserId, string threadId, string userId)
    {
        var pk = "user:"+threadUserId;
        var id = $"like:{threadId}:user:{userId}";
        return new DocumentKey(pk, id);
    }
}