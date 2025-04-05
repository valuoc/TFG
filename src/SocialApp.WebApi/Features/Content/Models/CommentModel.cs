using SocialApp.WebApi.Data.User;

namespace SocialApp.WebApi.Features.Content.Models;

public sealed class CommentModel
{
    public string UserId { get; set; }
    public string CommentId  { get; set; }
    public string Content { get; set; }
    public DateTime LastModify { get; set; }
    public int ViewCount { get; set; }
    public int CommentCount { get; set; }
    public int LikeCount { get; set; }
    
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
}