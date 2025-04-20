using SocialApp.WebApi.Data._Shared;

namespace SocialApp.WebApi.Data.User;

public record CommentLikeDocument(string ConversationUserId, string ConversationId, string CommentId, string UserId, bool Like) 
    : Document(Key(ConversationUserId, ConversationId, CommentId, UserId))
{
    public static DocumentKey Key(string conversationUserId, string conversationId, string commentId, string userId)
    {
        var pk = "user:"+conversationUserId;
        var id = $"like:{conversationId}:comment:{commentId}:user:{userId}";
        return new DocumentKey(pk, id);
    }
}