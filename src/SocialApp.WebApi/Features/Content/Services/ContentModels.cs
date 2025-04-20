using SocialApp.Models.Content;
using SocialApp.WebApi.Data.User;

namespace SocialApp.WebApi.Features.Content.Services;

public static class ContentModels
{
    public static ConversationHeaderModel From(FeedConversationDocument conversation)
        => new()
        {
            UserId = conversation.FeedUserId,
            ConversationId = conversation.ConversationId,
            Content = conversation.Content,
            LastModify = conversation.LastModify
        };

    public static CommentModel From(CommentDocument comment)
    {
        return new CommentModel
        {
            UserId = comment.UserId,
            CommentId = comment.CommentId,
            Content = comment.Content,
            LastModify = comment.LastModify
        };
    }

    public static void Apply(CommentModel commentModel, CommentCountsDocument commentCount)
    {
        commentModel.CommentCount = commentCount.CommentCount;
        commentModel.ViewCount = commentCount.ViewCount;
        commentModel.LikeCount = commentCount.LikeCount;
    }

    public static ConversationModel From(ConversationDocument? conversation)
        => new()
        {
            UserId = conversation.UserId,
            ConversationId = conversation.ConversationId,
            Content = conversation.Content,
            LastModify = conversation.LastModify,
            LastComments = new List<CommentModel>()
        };
}