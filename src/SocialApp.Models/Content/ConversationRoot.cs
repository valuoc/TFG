namespace SocialApp.Models.Content;

public class ConversationRoot
{
    public string Handle { get; set; }
    public string ConversationId  { get; set; }
    public string Content { get; set; }
    public DateTime LastModify { get; set; }
    public int ViewCount { get; set; }
    public int CommentCount { get; set; }
    public int LikeCount { get; set; }
}