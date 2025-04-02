using Microsoft.Azure.Cosmos;
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

    private ContentContainer GetContentsContainer()
        => new(_userDb);
    
    private PendingDocumentsContainer GetPendingDocumentsContainer()
        => new(_userDb);
    
    public async ValueTask<string> CreatePostAsync(UserSession user, string content, OperationContext context)
    {
        try
        {
            var postId = Ulid.NewUlid(context.UtcNow).ToString();
            var contents = GetContentsContainer();
            var thread = new ThreadDocument(user.UserId, postId, content, context.UtcNow.UtcDateTime, 0, null, null) { IsRootThread = true };
            await contents.CreateThreadAsync(thread, context);
            return postId;
        }
        catch (CosmosException e)
        {
            throw new ContentException(ContentError.UnexpectedError, e);
        }
    }
    
    public async ValueTask<string> CreateCommentAsync(UserSession user, string threadUserId, string threadId, string content, OperationContext context)
    {
        try
        {
            var commentId = Ulid.NewUlid(context.UtcNow).ToString();
            var contents = GetContentsContainer();
            
            var comment = new CommentDocument(threadUserId, threadId, user.UserId, commentId, content, context.UtcNow.UtcDateTime, 0);
            var thread = new ThreadDocument(user.UserId, commentId, content, context.UtcNow.UtcDateTime, 0, threadUserId, threadId);
            
            context.Signal("create-comment");
            await contents.CreateCommentAsync(comment, context);

            try
            {
                context.Signal("create-comment-post");
                await contents.CreateThreadAsync(thread, context);
            }
            catch (CosmosException)
            {
                // Change feed will correct this
                // log?
            }
            
            return commentId;
        }
        catch (Exception e)
        {
            throw new ContentException(ContentError.UnexpectedError, e);
        }
    }

    public async ValueTask UpdateThreadAsync(UserSession user, string threadId, string content, OperationContext context)
    {
        try
        {
            var contents = GetContentsContainer();

            var (thread, _) = await contents.GetPostDocumentAsync(user.UserId, threadId, context);
            
            if(thread == null)
                throw new ContentException(ContentError.ContentNotFound);
            
            thread = thread with { Content = content, Version = thread.Version + 1 };
            
            context.Signal("update-post");
            await contents.ReplaceDocumentAsync(thread, context);

            if (!string.IsNullOrWhiteSpace(thread.ParentThreadUserId))
            {
                try
                {
                    context.Signal("update-comment");
                    var comment = new CommentDocument(thread.ParentThreadUserId, thread.ParentThreadId, thread.UserId, thread.ThreadId, thread.Content, thread.LastModify, thread.Version);
                    await contents.ReplaceDocumentAsync(comment, context);
                }
                catch (CosmosException e)
                {
                    // Change Feed will fix this
                    // Log ?
                }
            }
        }
        catch (CosmosException e)
        {
            throw new ContentException(ContentError.UnexpectedError, e);
        }
    }
    
    public async ValueTask DeleteThreadAsync(UserSession user, string threadId, OperationContext context)
    {
        await ReconcilePendingOperationsAsync(user, context);

        try
        {
            var contents = GetContentsContainer();
            var pendings = GetPendingDocumentsContainer();
        
            var (thread, _) = await contents.GetPostDocumentAsync(user.UserId, threadId, context);
            
            PendingOperationsDocument? pending = null;
            CommentDocument? comment = null;
            PendingOperation? operation = null;
            if (!string.IsNullOrWhiteSpace(thread.ParentThreadUserId))
            {
                comment = await contents.GetCommentAsync(thread.ParentThreadUserId, thread.ParentThreadId, thread.ThreadId, context);
                var pendingData = new [] {thread.ParentThreadUserId, thread.ParentThreadId, threadId};
                operation = new PendingOperation(threadId, PendingOperationName.DeleteComment, context.UtcNow.UtcDateTime, pendingData);
                pending = await pendings.RegisterPendingOperationAsync(user, operation, context);
            }
        
            context.Signal("delete-post");
            await contents.RemoveThreadAsync(thread, context);

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
    
    public async ValueTask<ThreadWithComments> GetThreadAsync(UserSession user, string postId, int lastCommentCount, OperationContext context)
    {
        await ReconcilePendingOperationsAsync(user, context);
        
        var contents = GetContentsContainer();
        try
        {
            var documents = await contents.GetAllThreadDocumentsAsync(user, postId, lastCommentCount, context);
            if(documents.Post == null)
            {
                var pendings = GetPendingDocumentsContainer();
                if(await TryRecoverThreadDocumentsAsync(pendings, user, postId, context))
                    documents = await contents.GetAllThreadDocumentsAsync(user, postId, lastCommentCount, context);
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
    
    public async ValueTask<IReadOnlyList<ThreadWithComments>> GetUserPostsAsync(UserSession user, string? afterPostId, int limit, OperationContext context)
    {
        await ReconcilePendingOperationsAsync(user, context);
        
        var contents = GetContentsContainer();

        try
        {
            var posts = await contents.GetUserThreadsAsync(user.UserId, afterPostId, limit, context);
            if (posts == null)
                return Array.Empty<ThreadWithComments>();
        
            var postsModels = new List<ThreadWithComments>(posts.Count);
            for (var i = 0; i < posts.Count; i++)
            {
                var thread = ThreadWithComments.From(posts[i].Item1);
                thread.CommentCount = posts[i].Item2.CommentCount;
                thread.ViewCount = posts[i].Item2.ViewCount;
                thread.LikeCount = posts[i].Item2.LikeCount;
                postsModels.Add(thread);
            }
            return postsModels;
        }
        catch (CosmosException e)
        {
            throw new ContentException(ContentError.UnexpectedError, e);
        }
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
        var threadId = pending.Data[1];
        var (thread, _) = await contents.GetPostDocumentAsync(user.UserId, threadId, context);
        
        if (thread == null)
            return false;
        
        var comment = await contents.GetCommentAsync(thread.ParentThreadUserId, thread.ParentThreadId, threadId, context);
        
        if (comment == null)
            return false;

        if (comment.Version >= thread.Version)
            return false;
        
        comment = comment with { Content = thread.Content, Version = thread.Version};
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
            var post = new ThreadDocument(user.UserId, postId, comment.Content, context.UtcNow.UtcDateTime, 0, parentUserId, parentPostId);
            var postCounts = new ThreadCountsDocument(user.UserId, postId, 0, 0, 0, parentUserId, parentPostId);
            
            // Because it was missing, it cannot have comments, views or likes
            await contents.CreateThreadAsync(post, context);
            return true;
        }

        return false;
    }

    private async ValueTask<bool> TryRecoverThreadDocumentsAsync(PendingDocumentsContainer pendings, UserSession user, string postId, OperationContext context)
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
    
    private static ThreadWithComments? BuildPostModel(ThreadDocument thread, ThreadCountsDocument threadCounts, List<CommentDocument>? comments, List<CommentCountsDocument>? commentCounts)
    {
        var model = ThreadWithComments.From(thread);
        model.CommentCount = threadCounts.CommentCount;
        model.ViewCount = threadCounts.ViewCount +1;
        model.LikeCount = threadCounts.LikeCount;
        
        if (comments != null)
        {
            if (commentCounts == null)
                throw new InvalidOperationException($"Comments of post {thread.UserId}/{thread.ThreadId} are present but comment counts is null.");
            
            for (var i = 0; i < comments.Count; i++)
            {
                var commentDocument = comments[i];
                var commentCountsDocument = commentCounts[i];
                
                var comment = Comment.From(commentDocument);
                if (comment.PostId != commentCountsDocument.CommentId)
                    throw new InvalidOperationException($"The comment {commentDocument.CommentId} does not match the counts.");
                
                Comment.Apply(comment, commentCountsDocument);
                model.LastComments.Add(comment);
            }
            model.LastComments.Reverse();
        }

        return model;
    }
}