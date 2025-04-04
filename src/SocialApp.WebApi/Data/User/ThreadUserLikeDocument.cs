using SocialApp.WebApi.Data._Shared;

namespace SocialApp.WebApi.Data.User;

public record ThreadUserLikeDocument(string UserId, string ThreadUserId, string ThreadId, bool Like, string? ParentThreadUserId, string? ParentThreadId) 
    : Document(Key(UserId, ThreadUserId, ThreadId))
{
    public static DocumentKey Key(string userId, string threadUserId, string threadId)
    {
        var pk = "user:"+userId;
        var id = $"like:{threadUserId}:thread:{threadId}";
        return new DocumentKey(pk, id);
    }
}