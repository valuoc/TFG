using SocialApp.WebApi.Data._Shared;

namespace SocialApp.WebApi.Data.User;

public record ConversationUserLikeDocument(string UserId, string ConversationUserId, string ConversationId, bool Like, string? ParentConversationUserId, string? ParentConversationId) 
    : Document(Key(UserId, ConversationUserId, ConversationId))
{
    public static DocumentKey Key(string userId, string conversationUserId, string conversationId)
    {
        var pk = "user:"+userId;
        var id = $"like:{conversationUserId}:conversation:{conversationId}";
        return new DocumentKey(pk, id);
    }
}