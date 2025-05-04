using SocialApp.WebApi.Data._Shared;

namespace SocialApp.WebApi.Data.User;

public record CommentDocument(string ConversationUserId, string ConversationId, string UserId, string CommentId, string Content, DateTime LastModify, int Version) 
    : Document(Key(ConversationUserId, ConversationId, CommentId))
{
    public static DocumentKey Key(string conversationUserId, string conversationId, string commentId)
    {
        var pk = "user:"+conversationUserId;
        var id = $"conversation:{conversationId}:comment:{commentId}";
        return new DocumentKey(pk, id);
    }
    
    public CommentCountsDocument CreateCounts()
    {
        return new CommentCountsDocument(this.ConversationUserId, this.ConversationId, this.UserId, this.CommentId, 0, 0, 0);
    }
}