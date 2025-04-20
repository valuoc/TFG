namespace SocialApp.Models.Content;

public sealed class CommentModel
{
    public string UserId { get; set; }
    public string CommentId  { get; set; }
    public string Content { get; set; }
    public DateTime LastModify { get; set; }
    public int ViewCount { get; set; }
    public int CommentCount { get; set; }
    public int LikeCount { get; set; }
    
}