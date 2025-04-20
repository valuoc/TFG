namespace SocialApp.Models.Content;

public class ConversationModel : ConversationHeaderModel
{
    public List<CommentModel> LastComments { get; set; }
}