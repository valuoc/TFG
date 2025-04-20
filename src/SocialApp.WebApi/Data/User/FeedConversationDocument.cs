using SocialApp.WebApi.Data._Shared;

namespace SocialApp.WebApi.Data.User;

public record FeedConversationDocument(string FeedUserId, string ConversationUserId, string ConversationId, string Content, DateTime LastModify, int Version) 
    : Document(Key(FeedUserId, ConversationUserId, ConversationId))
{
    public static FeedConversationDocument From(string feedUserId, ConversationDocument conversation)
        => new(feedUserId, conversation.UserId, conversation.ConversationId, conversation.Content, conversation.LastModify, conversation.Version)
        {
            Deleted = conversation.Deleted
        };

    public static DocumentKey Key(string userId, string conversationUserId, string conversationId)
    {
        var pk = "user:"+userId;
        var id = $"feed:{conversationId}:{conversationUserId}:conversation";
        return new DocumentKey(pk, id);
    }
    
    public static DocumentKey KeyUserFeedEnd(string userId)
    {
        var pk = "user:"+userId;
        var id = "feed:z";
        return new DocumentKey(pk, id);
    }
    
    public static DocumentKey KeyUserFeedStart(string userId)
    {
        var pk = "user:"+userId;
        var id = $"feed:";
        return new DocumentKey(pk, id);
    }
    
    public static DocumentKey KeyUserFeedFrom(string userId, string conversationId)
    {
        var pk = "user:"+userId;
        var id = $"feed:{conversationId}:";
        return new DocumentKey(pk, id);
    }
}