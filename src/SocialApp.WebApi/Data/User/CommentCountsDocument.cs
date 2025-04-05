using SocialApp.WebApi.Data.Shared;

namespace SocialApp.WebApi.Data.User;

public record CommentCountsDocument(string ConversationUserId, string ConversationId, string UserId, string CommentId, int LikeCount, int CommentCount, int ViewCount) 
    : Document(Key(ConversationUserId, ConversationId, CommentId))
{
    public static DocumentKey Key(string conversationUserId, string conversationId, string commentId)
    {
        var pk = "user:"+conversationUserId;
        var id = $"conversation:{conversationId}:comment:{commentId}:counts";
        return new DocumentKey(pk, id);
    }

    public static CommentCountsDocument? TryGenerateParentCommentCounts(ConversationCountsDocument tcounts)
        => string.IsNullOrWhiteSpace(tcounts.ParentConversationId) 
            ? null 
            : new (tcounts.ParentConversationUserId, tcounts.ParentConversationId, tcounts.UserId, tcounts.ConversationId,
                tcounts.LikeCount, tcounts.CommentCount, tcounts.ViewCount)
                {
                    Deleted = tcounts.Deleted,
                    Ttl = tcounts.Ttl
                };
}