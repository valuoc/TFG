using SocialApp.WebApi.Data.User;
using SocialApp.WebApi.Features._Shared.Services;
using SocialApp.WebApi.Features.Content.Containers;
using SocialApp.WebApi.Features.Content.Exceptions;
using SocialApp.WebApi.Features.Content.Models;
using SocialApp.WebApi.Features.Session.Models;

namespace SocialApp.WebApi.Features.Content.Services;

public sealed class ContentService
{
    private readonly UserDatabase _userDb;
    public ContentService(UserDatabase userDb)
        => _userDb = userDb;

    private ContentContainer GetContainer()
        => new ContentContainer(_userDb);
    
    public async ValueTask<string> CreatePostAsync(UserSession user, string content, OperationContext context)
    {
        var postId = Ulid.NewUlid(context.UtcNow).ToString();
        var contents = GetContainer();
        var post = new PostDocument(user.UserId, postId, content, context.UtcNow.UtcDateTime, 0, null, null);
        var postCounts = new PostCountsDocument(user.UserId, postId, 0, 0, 0, null, null);
        await contents.CreatePostAsync(post, postCounts, context);
        return postId;
    }
    
    public async ValueTask<string> CreateCommentAsync(UserSession user, string parentUserId, string parentPostId, string content, OperationContext context)
    {
        try
        {
            var postId = Ulid.NewUlid(context.UtcNow).ToString();
            var contents = GetContainer();
            
            var comment = new CommentDocument(user.UserId, postId, parentUserId, parentPostId, content, context.UtcNow.UtcDateTime, 0);
            var commentCounts = new CommentCountsDocument(user.UserId, postId, parentUserId, parentPostId, 0, 0, 0);

            var post = new PostDocument(user.UserId, postId, content, context.UtcNow.UtcDateTime, 0, parentUserId, parentPostId);
            var postCounts = new PostCountsDocument(user.UserId, postId, 0, 0, 0, parentUserId, parentPostId);

            var pendingData = new [] {parentUserId, parentPostId, postId};
            var operation = new PendingOperation(postId, "SyncCommentToPost", context.UtcNow.UtcDateTime, pendingData);
            var pending = await contents.RegisterPendingOperationAsync(user, operation, context);
            
            context.Signal("create-comment");
            await contents.CreateCommentAsync(comment, commentCounts, context);
            
            context.Signal("create-comment-post");
            await contents.CreatePostAsync(post, postCounts, context);
            
            context.Signal("update-parent-post");
            await contents.UpdateCommentCountsAsync(parentUserId, parentPostId, context);
            context.Signal("clear-pending-comment");
            
            await contents.ClearPendingOperationAsync(user, pending, operation, context);
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
        var contents = GetContainer();
        
        var document = await contents.GetPostDocumentAsync(user, postId, context);
        if(document == null)
        {
            var recovered = await TryRecoverPostDocumentsAsync(contents, user, postId, context);
            document = recovered.Post;

            if(document ==null)
                throw new ContentException(ContentError.ContentNotFound);
        }

        var updated = document with { Content = content, Version = document.Version + 1 };
        
        PendingOperationsDocument? pending = null;
        CommentDocument? comment = null;
        PendingOperation? operation = null;
        if (!string.IsNullOrWhiteSpace(document.CommentUserId))
        {
            comment = await contents.GetCommentAsync(updated.CommentUserId, updated.CommentPostId, updated.PostId, context);
            var pendingData = new [] {user.UserId, postId};
            operation = new PendingOperation(postId, "SyncPostToComment", context.UtcNow.UtcDateTime, pendingData);
            pending = await contents.RegisterPendingOperationAsync(user, operation, context);
        }
        
        await contents.ReplaceDocumentAsync(updated, context);

        if (pending != null)
        {
            if(comment.Version >= updated.Version)
                return;
            
            comment = comment with { Content = updated.Content, Version = updated.Version};
            await contents.ReplaceDocumentAsync(comment, context);
            await contents.ClearPendingOperationAsync(user, pending, operation, context);
        }
    }

    public async ValueTask<Post> GetPostAsync(UserSession user, string postId, int lastCommentCount, OperationContext context)
    {
        var contents = GetContainer();
        
        var documents = await contents.GetAllPostDocumentsAsync(user, postId, lastCommentCount, context);
        if(documents.Post == null)
        {
            documents = await TryRecoverPostDocumentsAsync(contents, user, postId, context);
            if(documents.Post == null)
                throw new ContentException(ContentError.ContentNotFound);
        }

        await contents.IncreaseViewsAsync(user.UserId, postId, context);
        return BuildPostModel(documents.Post, documents.PostCounts, documents.Comments, documents.CommentCounts);
    }
    
    public async ValueTask<IReadOnlyList<Comment>> GetPreviousCommentsAsync(string userId, string postId, string commentId, int lastCommentCount, OperationContext context)
    {
        var contents = GetContainer();
        var (comments, commentCounts) = await contents.GetPreviousCommentsAsync(userId, postId, commentId, lastCommentCount, context);
        if (comments == null)
            return Array.Empty<Comment>();

        return BuildCommentList(comments, commentCounts);
    }
    
    private async Task<AllPostDocuments> TryRecoverPostDocumentsAsync(ContentContainer contents, UserSession user, string postId, OperationContext context)
    {
        var pendingDocument = await contents.GetPendingOperationsAsync(user.UserId, context);
        var pending = pendingDocument?.Items?.SingleOrDefault(x => x.Id == postId);
        if (pending != null)
        {
            var parentUserId = pending.Data[0];
            var parentPostId = pending.Data[1];
            
            var comment = await contents.GetCommentAsync(parentUserId, parentPostId, postId, context);
            
            var post = new PostDocument(user.UserId, postId, comment.Content, context.UtcNow.UtcDateTime, 0, parentUserId, parentPostId);
            var postCounts = new PostCountsDocument(user.UserId, postId, 0, 0, 0, parentUserId, parentPostId);
            
            // If pending is retried, parent counts could be updated more than once. 
            await contents.UpdateCommentCountsAsync(parentUserId, parentPostId, context);
            var postDocuments = await contents.CreatePostAsync(post, postCounts,  context);
            
            await contents.ClearPendingOperationAsync(user, pendingDocument!, pending!, context);
                
            // Because it was missing, it cannot have comments, views or likes
            return postDocuments;
        }

        return new AllPostDocuments(null, null, null, null);
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
        var contents = GetContainer();
        
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