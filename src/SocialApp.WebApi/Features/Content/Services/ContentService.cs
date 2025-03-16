using SocialApp.WebApi.Features.Content.Databases;
using SocialApp.WebApi.Features.Content.Documents;
using SocialApp.WebApi.Features.Content.Exceptions;
using SocialApp.WebApi.Features.Content.Models;
using SocialApp.WebApi.Features.Services;
using SocialApp.WebApi.Features.Session.Models;

namespace SocialApp.WebApi.Features.Content.Services;

public sealed class ContentService
{
    private readonly ContentDatabase _contentDb;
    public ContentService(ContentDatabase contentDb)
        => _contentDb = contentDb;

    public async ValueTask<string> CreatePostAsync(UserSession user, string content, OperationContext context)
    {
        var postId = Ulid.NewUlid(context.UtcNow).ToString();
        var contents = _contentDb.GetContentContainer();
        await contents.CreatePostAsync(user.UserId, null, null, postId, content, context);
        return postId;
    }
    
    public async ValueTask<string> CreateCommentAsync(UserSession user, string parentUserId, string parentPostId, string content, OperationContext context)
    {
        try
        {
            var postId = Ulid.NewUlid(context.UtcNow).ToString();
            var contents = _contentDb.GetContentContainer();

            var pending = await contents.RegisterPendingCommentActionAsync(user.UserId, parentUserId, parentPostId, postId, PendingCommentOperation.Add, context);
            context.Signal("create-comment");
            await contents.CreateCommentAsync(user.UserId, parentUserId, parentPostId, content, postId, context);
            context.Signal("create-comment-post");
            await contents.CreatePostAsync(user.UserId, parentUserId, parentPostId, postId, content, context);
            context.Signal("update-parent-post");
            await contents.UpdateCommentCountsAsync(parentUserId, parentPostId, context);
            context.Signal("clear-pending-comment");
            await contents.ClearPendingCommentActionAsync(pending, postId, context);
            return postId;
        }
        catch (ContentException)
        {
            throw;
        }
        catch (Exception e)
        {
            throw new ContentException(ContentError.UnexpectedError, e);
        }
    }

    public async ValueTask UpdatePostAsync(UserSession user, string postId, string content, OperationContext context)
    {
        var contents = _contentDb.GetContentContainer();
        
        var document = await contents.GetPostDocumentAsync(user.UserId, postId, context);
        if(document == null)
            throw new ContentException(ContentError.ContentNotFound);
        
        PendingCommentsDocument? pending = null;
        if (!string.IsNullOrWhiteSpace(document.CommentUserId))
            pending = await contents.RegisterPendingCommentActionAsync(user.UserId, document.CommentUserId, document.CommentPostId, postId, PendingCommentOperation.Add, context);

        var updated = document with { Content = content, Version = document.Version + 1 };
        await contents.ReplaceDocumentAsync(updated, context);

        if (pending != null)
        {
            await UpdateCommentAsync(contents, updated, context);
            await contents.ClearPendingCommentActionAsync(pending, postId, context);
        }
    }

    private async ValueTask UpdateCommentAsync(ContentContainer contents, PostDocument updated, OperationContext context)
    {
        var comment = await contents.GetCommentAsync(updated.CommentUserId, updated.CommentPostId, updated.PostId, context);
        if(comment.Version >= updated.Version)
            return;
        comment = comment with { Content = updated.Content, Version = updated.Version};
        await contents.ReplaceDocumentAsync(comment, context);
    }

    public async ValueTask<Post> GetPostAsync(UserSession user, string postId, int lastCommentCount, OperationContext context)
    {
        var contents = _contentDb.GetContentContainer();
        
        var documents = await contents.GetAllPostDocumentsAsync(user.UserId, postId, lastCommentCount, context);
        if(documents.Post == null)
            throw new ContentException(ContentError.ContentNotFound);

        await contents.IncreaseViewsAsync(user.UserId, postId, context);
        return BuildPostModel(documents.Post, documents.PostCounts, documents.Comments, documents.CommentCounts);
    }
    
    public async ValueTask<IReadOnlyList<Comment>> GetPreviousCommentsAsync(string userId, string postId, string commentId, int lastCommentCount, OperationContext context)
    {
        var contents = _contentDb.GetContentContainer();
        var (comments, commentCounts) = await contents.GetPreviousCommentsAsync(userId, postId, commentId, lastCommentCount, context);
        if (comments == null)
            return Array.Empty<Comment>();

        return BuildCommentList(comments, commentCounts);
    }
    
    private static IReadOnlyList<Comment> BuildCommentList(List<CommentDocument> comments, List<CommentCountsDocument>? commentCounts)
    {
        var commentModels = new List<Comment>(comments.Count);
        for (var i = 0; i < comments.Count; i++)
        {
            var comment = Comment.From(comments[i]);
            var commentCount = commentCounts[i];
            Comment.Apply(comment, commentCount);
            commentModels.Add(comment);
        }
        commentModels.Reverse();
        return commentModels;
    }

    public async ValueTask<IReadOnlyList<Post>> GetUserPostsAsync(string userId, string? afterPostId, int limit, OperationContext context)
    {
        var contents = _contentDb.GetContentContainer();
        
        var posts = await contents.GetUserPostsAsync(userId, afterPostId, limit, context);
        if (posts == null)
            return Array.Empty<Post>();
        
        var postsModels = new List<Post>(posts.Count);
        for (var i = 0; i < posts.Count; i++)
        {
            var post = Post.From(posts[i]);
            postsModels.Add(post);
        }
        return postsModels;
    }

    private static Post? BuildPostModel(PostDocument post, PostCountsDocument postCounts, List<CommentDocument>? comments, List<CommentCountsDocument>? commentCounts)
    {
        var model = Post.From(post);
        model.CommentCount = postCounts.CommentCount;
        model.ViewCount = postCounts.ViewCount +1;
        model.LikeCount = postCounts.LikeCount;
        
        if (comments != null)
        {
            if (commentCounts == null)
                throw new InvalidOperationException($"Comments of post {post.UserId}/{post.PostId} are present but comment counts is null.");
            
            for (var i = 0; i < comments.Count; i++)
            {
                var commentDocument = comments[i];
                var commentCountsDocument = commentCounts[i];
                
                var comment = Comment.From(commentDocument);
                if (comment.PostId != commentCountsDocument.PostId)
                    throw new InvalidOperationException($"The comment {commentDocument.PostId} does not match the counts.");
                
                Comment.Apply(comment, commentCountsDocument);
                model.LastComments.Add(comment);
            }
            model.LastComments.Reverse();
        }

        return model;
    }
}