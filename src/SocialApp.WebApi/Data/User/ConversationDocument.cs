using SocialApp.WebApi.Data._Shared;

namespace SocialApp.WebApi.Data.User;

public record ConversationDocument(string UserId, string ConversationId, string Content, DateTime LastModify, int Version, string? ParentConversationUserId, string? ParentConversationId) 
    : Document(Key(UserId, ConversationId))
{
    public bool IsRootConversation {get; set;}

    public static DocumentKey Key(string userId, string conversationId)
    {
        var pk = "user:"+userId;
        var id = $"conversation:{conversationId}:z_conversation";
        return new DocumentKey(pk, id);
    }
    
    public static DocumentKey KeyUserConversationsEnd(string userId)
    {
        var pk = "user:"+userId;
        var id = "conversation:z";
        return new DocumentKey(pk, id);
    }
    
    public static DocumentKey KeyConversationsItemsStart(string userId, string conversationId)
    {
        var pk = "user:"+userId;
        var id = $"conversation:{conversationId}";
        return new DocumentKey(pk, id);
    }
    
    public ConversationCountsDocument CreateCounts()
    {
        return new ConversationCountsDocument(this.UserId, this.ConversationId, 0, 0, 0, this.ParentConversationUserId, this.ParentConversationId)
        {
            IsRootConversation = this.IsRootConversation
        };
    }
}