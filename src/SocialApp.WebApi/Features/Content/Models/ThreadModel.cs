using SocialApp.WebApi.Data.User;

namespace SocialApp.WebApi.Features.Content.Models;

public class ThreadHeaderModel
{
    public string UserId { get; set; }
    public string ThreadId  { get; set; }
    public string Content { get; set; }
    public DateTime LastModify { get; set; }
    public int ViewCount { get; set; }
    public int CommentCount { get; set; }
    public int LikeCount { get; set; }

    public static ThreadHeaderModel From(FeedThreadDocument thread)
        => new()
        {
            UserId = thread.FeedUserId,
            ThreadId = thread.ThreadId,
            Content = thread.Content,
            LastModify = thread.LastModify
        };
}

public class ThreadModel : ThreadHeaderModel
{
    public List<Comment> LastComments { get; set; }

    public static ThreadModel From(ThreadDocument? thread)
        => new()
        {
            UserId = thread.UserId,
            ThreadId = thread.ThreadId,
            Content = thread.Content,
            LastModify = thread.LastModify,
            LastComments = new List<Comment>()
        };
}

public sealed class Comment
{
    public string UserId { get; set; }
    public string CommentId  { get; set; }
    public string Content { get; set; }
    public DateTime LastModify { get; set; }
    public int ViewCount { get; set; }
    public int CommentCount { get; set; }
    public int LikeCount { get; set; }
    
    public static Comment From(CommentDocument comment)
    {
        return new Comment
        {
            UserId = comment.UserId,
            CommentId = comment.CommentId,
            Content = comment.Content,
            LastModify = comment.LastModify
        };
    }
    
    public static void Apply(Comment comment, CommentCountsDocument commentCount)
    {
        comment.CommentCount = commentCount.CommentCount;
        comment.ViewCount = commentCount.ViewCount;
        comment.LikeCount = commentCount.LikeCount;
    }
}