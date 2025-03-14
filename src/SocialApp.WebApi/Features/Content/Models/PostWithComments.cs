using SocialApp.WebApi.Features.Content.Documents;

namespace SocialApp.WebApi.Features.Content.Models;

public sealed class PostWithComments
{
    public string UserId { get; set; }
    public string PostId  { get; set; }
    public string Content { get; set; }
    public List<PostComment> Comments { get; set; }
    public int ViewCount { get; set; }
    public int CommentCount { get; set; }
    public int LikeCount { get; set; }

    public static PostWithComments? From(PostDocument? post)
    {
        return new PostWithComments()
        {
            UserId = post.UserId,
            PostId = post.PostId,
            Content = post.Content,
            Comments = new List<PostComment>()
        };
    }
}

public sealed class PostComment
{
    public string UserId { get; set; }
    public string PostId  { get; set; }
    public string Content { get; set; }
    
    public static PostComment From(CommentDocument comment)
    {
        return new PostComment()
        {
            UserId = comment.UserId,
            PostId = comment.PostId,
            Content = comment.Content
        };
    }
}