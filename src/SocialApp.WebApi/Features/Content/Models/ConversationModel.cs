using SocialApp.WebApi.Data.User;

namespace SocialApp.WebApi.Features.Content.Models;

public class ConversationModel : ConversationHeaderModel
{
    public List<CommentModel> LastComments { get; set; }

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