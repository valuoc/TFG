using SocialApp.WebApi.Data._Shared;

namespace SocialApp.WebApi.Data.User;

public record ConversationCountsDocument(string UserId, string ConversationId, int LikeCount, int CommentCount, int ViewCount, string? ParentConversationUserId, string? ParentConversationId) 
    : Document(Key(UserId, ConversationId))
{
    public bool IsRootConversation {get; set;}
    
    public static DocumentKey Key(string userId, string conversationId)
    {
        var pk = "user:"+userId;
        var id = $"conversation:{conversationId}:conversation_counts";
        return new DocumentKey(pk, id);
    }

    public bool AllCountersAreZero()
        => CommentCount == 0 && LikeCount == 0 && ViewCount == 0;
}