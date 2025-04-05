using SocialApp.WebApi.Data.User;

namespace SocialApp.WebApi.Features.Content.Models;

public class ConversationHeaderModel
{
    public string UserId { get; set; }
    public string ConversationId  { get; set; }
    public string Content { get; set; }
    public DateTime LastModify { get; set; }
    public int ViewCount { get; set; }
    public int CommentCount { get; set; }
    public int LikeCount { get; set; }

    public static ConversationHeaderModel From(FeedConversationDocument conversation)
        => new()
        {
            UserId = conversation.FeedUserId,
            ConversationId = conversation.ConversationId,
            Content = conversation.Content,
            LastModify = conversation.LastModify
        };
}