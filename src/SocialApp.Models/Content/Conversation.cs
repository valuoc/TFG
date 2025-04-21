namespace SocialApp.Models.Content;

public class Conversation
{
    public ConversationRoot Root { get; set; }
    public List<ConversationComment> LastComments { get; set; }
}