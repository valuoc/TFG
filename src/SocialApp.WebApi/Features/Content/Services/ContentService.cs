using Microsoft.Azure.Cosmos;
using SocialApp.WebApi.Data.User;
using SocialApp.WebApi.Features._Shared.Services;
using SocialApp.WebApi.Features.Content.Containers;
using SocialApp.WebApi.Features.Content.Exceptions;
using SocialApp.WebApi.Features.Content.Models;
using SocialApp.WebApi.Features.Follow.Containers;
using SocialApp.WebApi.Features.Session.Models;

namespace SocialApp.WebApi.Features.Content.Services;

public sealed class ContentService
{
    private readonly UserDatabase _userDb;
    public ContentService(UserDatabase userDb)
        => _userDb = userDb;

    private ContentContainer GetContentsContainer()
        => new(_userDb);
    
    private PendingDocumentsContainer GetPendingDocumentsContainer()
        => new(_userDb);
    
    public async ValueTask<string> CreatePostAsync(UserSession user, string content, OperationContext context)
    {
        await ReconcilePendingOperationsAsync(user, context);
        try
        {
            var postId = Ulid.NewUlid(context.UtcNow).ToString();
            var contents = GetContentsContainer();
            var post = new PostDocument(user.UserId, postId, content, context.UtcNow.UtcDateTime, 0, null, null);
            await contents.CreatePostAsync(post, context);
            return postId;
        }
        catch (CosmosException e)
        {
            throw new ContentException(ContentError.UnexpectedError, e);
        }
    }
    
    public async ValueTask<string> CreateCommentAsync(UserSession user, string parentUserId, string parentPostId, string content, OperationContext context)
    {
        await ReconcilePendingOperationsAsync(user, context);
        try
        {
            var postId = Ulid.NewUlid(context.UtcNow).ToString();
            var contents = GetContentsContainer();
            var pendings = GetPendingDocumentsContainer();
            
            var comment = new CommentDocument(user.UserId, postId, parentUserId, parentPostId, content, context.UtcNow.UtcDateTime, 0);
            var post = new PostDocument(user.UserId, postId, content, context.UtcNow.UtcDateTime, 0, parentUserId, parentPostId);
            
            var pendingData = new [] {parentUserId, parentPostId, postId};
            var operation = new PendingOperation(postId, PendingOperationName.SyncCommentToPost, context.UtcNow.UtcDateTime, pendingData);
            
            var pending = await pendings.RegisterPendingOperationAsync(user, operation, context);
            
            context.Signal("create-comment");
            await contents.CreateCommentAsync(comment, context);
            
            context.Signal("create-comment-post");
            await contents.CreatePostAsync(post, context);
            
            context.Signal("update-parent-post");
            await contents.UpdateCommentCountsAsync(parentUserId, parentPostId, context);
            context.Signal("clear-pending-comment");
            
            await pendings.ClearPendingOperationAsync(user, pending, operation, context);
            return postId;
        }
        catch (Exception e)
        {
            throw new ContentException(ContentError.UnexpectedError, e);
        }
    }

    public async ValueTask UpdatePostAsync(UserSession user, string postId, string content, OperationContext context)
    {
        await ReconcilePendingOperationsAsync(user, context);

        try
        {
            var contents = GetContentsContainer();
            var pendings = GetPendingDocumentsContainer();
        
            var (post, _) = await GetOrRecoverPostDocumentAsync(user, postId, contents, pendings, context);
            
            var updated = post with { Content = content, Version = post.Version + 1 };
        
            PendingOperationsDocument? pending = null;
            CommentDocument? comment = null;
            PendingOperation? operation = null;
            if (!string.IsNullOrWhiteSpace(post.CommentUserId))
            {
                comment = await contents.GetCommentAsync(updated.CommentUserId, updated.CommentPostId, updated.PostId, context);
                var pendingData = new [] {user.UserId, postId};
                operation = new PendingOperation(postId, PendingOperationName.SyncPostToComment, context.UtcNow.UtcDateTime, pendingData);
                pending = await pendings.RegisterPendingOperationAsync(user, operation, context);
            }
        
            context.Signal("update-post");
            await contents.ReplaceDocumentAsync(updated, context);

            if (pending != null)
            {
                if(comment.Version >= updated.Version)
                    return;
            
                context.Signal("update-comment");
                comment = comment with { Content = updated.Content, Version = updated.Version};
                await contents.ReplaceDocumentAsync(comment, context);
                await pendings.ClearPendingOperationAsync(user, pending, operation, context);
            }
        }
        catch (CosmosException e)
        {
            throw new ContentException(ContentError.UnexpectedError, e);
        }
    }
    
    public async ValueTask DeletePostAsync(UserSession user, string postId, OperationContext context)
    {
        await ReconcilePendingOperationsAsync(user, context);

        try
        {
            var contents = GetContentsContainer();
            var pendings = GetPendingDocumentsContainer();
        
            var (post, _) = await GetOrRecoverPostDocumentAsync(user, postId, contents, pendings, context);
            
            PendingOperationsDocument? pending = null;
            CommentDocument? comment = null;
            PendingOperation? operation = null;
            if (!string.IsNullOrWhiteSpace(post.CommentUserId))
            {
                comment = await contents.GetCommentAsync(post.CommentUserId, post.CommentPostId, post.PostId, context);
                var pendingData = new [] {post.CommentUserId, post.CommentPostId, postId};
                operation = new PendingOperation(postId, PendingOperationName.DeleteComment, context.UtcNow.UtcDateTime, pendingData);
                pending = await pendings.RegisterPendingOperationAsync(user, operation, context);
            }
        
            context.Signal("delete-post");
            await contents.RemovePostAsync(post, context);

            if (pending != null)
            {
                context.Signal("delete-comment");
                await contents.RemoveCommentAsync(comment, context);
                await pendings.ClearPendingOperationAsync(user, pending, operation, context);
            }
        }
        catch (CosmosException e)
        {
            throw new ContentException(ContentError.UnexpectedError, e);
        }
    }
    
    public async ValueTask<PostWithComments> GetPostAsync(UserSession user, string postId, int lastCommentCount, OperationContext context)
    {
        await ReconcilePendingOperationsAsync(user, context);
        
        var contents = GetContentsContainer();
        try
        {
            var documents = await contents.GetAllPostDocumentsAsync(user, postId, lastCommentCount, context);
            if(documents.Post == null)
            {
                var pendings = GetPendingDocumentsContainer();
                if(await TryRecoverPostDocumentsAsync(pendings, user, postId, context))
                    documents = await contents.GetAllPostDocumentsAsync(user, postId, lastCommentCount, context);
                if(documents.Post == null)
                    throw new ContentException(ContentError.ContentNotFound);
            }

            await contents.IncreaseViewsAsync(user.UserId, postId, context);
            return BuildPostModel(documents.Post, documents.PostCounts, documents.Comments, documents.CommentCounts);
        }
        catch (CosmosException e)
        {
            throw new ContentException(ContentError.UnexpectedError, e);
        }
    }
    
    public async ValueTask<IReadOnlyList<Comment>> GetPreviousCommentsAsync(UserSession user, string postId, string commentId, int lastCommentCount, OperationContext context)
    {
        await ReconcilePendingOperationsAsync(user, context);

        try
        {
            var contents = GetContentsContainer();
            var (comments, commentCounts) = await contents.GetPreviousCommentsAsync(user.UserId, postId, commentId, lastCommentCount, context);
            if (comments == null)
                return Array.Empty<Comment>();

            return BuildCommentList(comments, commentCounts);
        }
        catch (CosmosException e)
        {
            throw new ContentException(ContentError.UnexpectedError, e);
        }
    }
    
    public async ValueTask<IReadOnlyList<PostWithComments>> GetUserPostsAsync(UserSession user, string? afterPostId, int limit, OperationContext context)
    {
        await ReconcilePendingOperationsAsync(user, context);
        
        var contents = GetContentsContainer();

        try
        {
            var posts = await contents.GetUserPostsAsync(user.UserId, afterPostId, limit, context);
            if (posts == null)
                return Array.Empty<PostWithComments>();
        
            var postsModels = new List<PostWithComments>(posts.Count);
            for (var i = 0; i < posts.Count; i++)
            {
                var post = PostWithComments.From(posts[i].Item1);
                post.CommentCount = posts[i].Item2.CommentCount;
                post.ViewCount = posts[i].Item2.ViewCount;
                post.LikeCount = posts[i].Item2.LikeCount;
                postsModels.Add(post);
            }
            return postsModels;
        }
        catch (CosmosException e)
        {
            throw new ContentException(ContentError.UnexpectedError, e);
        }
    }

    private async Task<(PostDocument?, PostCountsDocument?)> GetOrRecoverPostDocumentAsync(UserSession user, string postId, ContentContainer contents, PendingDocumentsContainer pendings, OperationContext context)
    {
        var (post, counts) = await contents.GetPostDocumentAsync(user, postId, context);
        
        if(post == null)
        {
            if(await TryRecoverPostDocumentsAsync(pendings, user, postId, context))
                (post, counts) = await contents.GetPostDocumentAsync(user, postId, context);

            if(post == null)
                throw new ContentException(ContentError.ContentNotFound);
        }

        return (post, counts);
    }

    private async ValueTask<bool> HandlePendingOperationAsync(UserSession user, PendingOperationsDocument pendingDocument, PendingOperation pending, OperationContext context)
    {
        var pendings = GetPendingDocumentsContainer();
        switch (pending.Name)
        {
            case PendingOperationName.SyncCommentToPost:
                await HandleSyncCommentToPostAsync(user, pending, context);
                await pendings.ClearPendingOperationAsync(user, pendingDocument!, pending!, context);
                return true;
            
            case PendingOperationName.SyncPostToComment:
                await HandleSyncPostToCommentAsync(user, pending, context);
                await pendings.ClearPendingOperationAsync(user, pendingDocument!, pending!, context);
                return true;
            
            case PendingOperationName.DeleteComment:
                await HandleDeleteCommentAsync(user, pending, context);
                await pendings.ClearPendingOperationAsync(user, pendingDocument!, pending!, context);
                return true;
        }
        
        return false;
    }

    private async ValueTask ReconcilePendingOperationsAsync(UserSession user, OperationContext context)
    {
        try
        {
            if (!user.HasPendingOperations)
                return;
        
            var pendings = GetPendingDocumentsContainer();
            var pendingDocument = await pendings.GetPendingOperationsAsync(user.UserId, context);
        
            if(pendingDocument?.Items == null)
                return;
        
            foreach (var pending in pendingDocument.Items)
            {
                await HandlePendingOperationAsync(user, pendingDocument, pending, context);
            }
        }
        catch (CosmosException e)
        {
            // TODO : Log
        }
    }
    
    // Recover from broken delete comment
    private async ValueTask HandleDeleteCommentAsync(UserSession user, PendingOperation pending, OperationContext context)
    {
        var contents = GetContentsContainer();
        var userId = pending.Data[0];
        var postId = pending.Data[1];
        var commentId = pending.Data[2];
        var comment = await contents.GetCommentAsync(userId, postId, commentId, context);
        if (comment == null)
            return;
        await contents.RemoveCommentAsync(comment, context);
    }

    // Recover from broken post update
    private async ValueTask<bool> HandleSyncPostToCommentAsync(UserSession user, PendingOperation pending, OperationContext context)
    {
        var contents = GetContentsContainer();
        
        var userId = pending.Data[0];
        var postId = pending.Data[1];
        var (post, _) = await contents.GetPostDocumentAsync(user, postId, context);
        
        if (post == null)
            return false;
        
        var comment = await contents.GetCommentAsync(post.CommentUserId, post.CommentPostId, postId, context);
        
        if (comment == null)
            return false;

        if (comment.Version >= post.Version)
            return false;
        
        comment = comment with { Content = post.Content, Version = post.Version};
        await contents.ReplaceDocumentAsync(comment, context);
        return true;
    }

    // Recover from broken comment creation
    private async ValueTask<bool> HandleSyncCommentToPostAsync(UserSession user, PendingOperation pending, OperationContext context)
    {
        var contents = GetContentsContainer();
        
        var parentUserId = pending.Data[0];
        var parentPostId = pending.Data[1];
        var postId = pending.Data[2];

        var comment = await contents.GetCommentAsync(parentUserId, parentPostId, postId, context);
        
        if(comment != null)
        {
            var post = new PostDocument(user.UserId, postId, comment.Content, context.UtcNow.UtcDateTime, 0, parentUserId, parentPostId);
            var postCounts = new PostCountsDocument(user.UserId, postId, 0, 0, 0, parentUserId, parentPostId);

            // If pending is retried, parent counts could be updated more than once. 
            await contents.UpdateCommentCountsAsync(parentUserId, parentPostId, context);
            
            // Because it was missing, it cannot have comments, views or likes
            await contents.CreatePostAsync(post, context);
            return true;
        }

        return false;
    }

    private async ValueTask<bool> TryRecoverPostDocumentsAsync(PendingDocumentsContainer pendings, UserSession user, string postId, OperationContext context)
    {
        var pendingDocument = await pendings.GetPendingOperationsAsync(user.UserId, context);
        var pending = pendingDocument?.Items?.SingleOrDefault(x => x.Id == postId);
        if (pending != null)
        {
            await HandleSyncCommentToPostAsync(user, pending, context);
            await pendings.ClearPendingOperationAsync(user, pendingDocument!, pending!, context);
            return true;
        }

        return false;
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
    
    private static PostWithComments? BuildPostModel(PostDocument post, PostCountsDocument postCounts, List<CommentDocument>? comments, List<CommentCountsDocument>? commentCounts)
    {
        var model = PostWithComments.From(post);
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