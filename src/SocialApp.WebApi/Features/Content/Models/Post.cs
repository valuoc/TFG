using SocialApp.WebApi.Features.Content.Documents;

namespace SocialApp.WebApi.Features.Content.Models;

public sealed class Post
{
    public string UserId { get; set; }
    public string PostId  { get; set; }
    public string Content { get; set; }
    public DateTime LastModify { get; set; }
    public List<Comment> LastComments { get; set; }
    public int ViewCount { get; set; }
    public int CommentCount { get; set; }
    public int LikeCount { get; set; }

    public static Post From(PostDocument? post)
    {
        return new Post()
        {
            UserId = post.UserId,
            PostId = post.PostId,
            Content = post.Content,
            LastModify = post.LastModify,
            LastComments = new List<Comment>()
        };
    }
}

public sealed class Comment
{
    public string UserId { get; set; }
    public string PostId  { get; set; }
    public string Content { get; set; }
    public DateTime LastModify { get; set; }
    public int ViewCount { get; set; }
    public int CommentCount { get; set; }
    public int LikeCount { get; set; }
    
    public static Comment From(CommentDocument comment)
    {
        return new Comment()
        {
            UserId = comment.UserId,
            PostId = comment.PostId,
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