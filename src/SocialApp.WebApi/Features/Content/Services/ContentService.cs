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
    
    public async Task<string> CreateThreadAsync(UserSession user, string content, OperationContext context)
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
    
    public async Task<string> CreateCommentAsync(UserSession user, string threadUserId, string threadId, string content, OperationContext context)
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

    public async Task UpdateThreadAsync(UserSession user, string threadId, string content, OperationContext context)
    {
        try
        {
            var contents = GetContentsContainer();

            var thread = await contents.GetThreadDocumentAsync(user.UserId, threadId, context);
            
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
    
    public async Task LikeThreadAsync(UserSession user, string userId, string threadId, OperationContext context)
    {
        try
        {
            var contents = GetContentsContainer();

            var thread = await contents.GetThreadDocumentAsync(user.UserId, threadId, context);
            
            if(thread == null)
                throw new ContentException(ContentError.ContentNotFound);
            
            context.Signal("like-post");
            //await contents.LikeThreadAsync(counts, context);

            if (!string.IsNullOrWhiteSpace(thread.ParentThreadUserId))
            {
                try
                {
                    context.Signal("like-comment");
                    //await contents.LikeCommentAsync(thread.ParentThreadUserId, thread.ParentThreadId, thread.ThreadId, context);
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
    
    public async Task DeleteThreadAsync(UserSession user, string threadId, OperationContext context)
    {
        try
        {
            var contents = GetContentsContainer();
        
            var thread = await contents.GetThreadDocumentAsync(user.UserId, threadId, context);
        
            context.Signal("delete-post");
            await contents.RemoveThreadAsync(thread, context);

            if (!string.IsNullOrWhiteSpace(thread.ParentThreadUserId))
            {
                try
                {
                    context.Signal("delete-comment");
                    await contents.RemoveCommentAsync(thread.ParentThreadUserId, thread.ParentThreadId, thread.ThreadId, context);
                }
                catch (CosmosException)
                {
                    // Change feed will correct this.
                    // log?
                }
            }
        }
        catch (CosmosException e)
        {
            throw new ContentException(ContentError.UnexpectedError, e);
        }
    }
    
    public async Task<ThreadModel> GetThreadAsync(UserSession user, string userId, string postId, int lastCommentCount, OperationContext context)
    {
        var contents = GetContentsContainer();
        try
        {
            var documents = await contents.GetAllThreadDocumentsAsync(userId, postId, lastCommentCount, context);
            if(documents.Thread == null)
                throw new ContentException(ContentError.ContentNotFound);

            await contents.IncreaseViewsAsync(user.UserId, postId, context);
            return BuildPostModel(documents.Thread, documents.ThreadCounts, documents.Comments, documents.CommentCounts);
        }
        catch (CosmosException e)
        {
            throw new ContentException(ContentError.UnexpectedError, e);
        }
    }
    
    public async Task<IReadOnlyList<Comment>> GetPreviousCommentsAsync(UserSession user, string userId, string postId, string commentId, int lastCommentCount, OperationContext context)
    {
        try
        {
            var contents = GetContentsContainer();
            var (comments, commentCounts) = await contents.GetPreviousCommentsAsync(userId, postId, commentId, lastCommentCount, context);
            if (comments == null)
                return Array.Empty<Comment>();

            return BuildCommentList(comments, commentCounts);
        }
        catch (CosmosException e)
        {
            throw new ContentException(ContentError.UnexpectedError, e);
        }
    }
    
    public async Task<IReadOnlyList<ThreadModel>> GetUserPostsAsync(UserSession user, string? afterPostId, int limit, OperationContext context)
    {
        var contents = GetContentsContainer();

        try
        {
            var (threads, threadCounts) = await contents.GetUserThreadsDocumentsAsync(user.UserId, afterPostId, limit, context);
            if (threads == null || threads.Count == 0)
                return Array.Empty<ThreadModel>();
            
            var sorted = threads
                .Join(threadCounts, i => i.ThreadId, o => o.ThreadId, (i, o) => (i, o))
                .OrderByDescending(x => x.i.Sk);

            var postsModels = new List<ThreadModel>(threads.Count);
            foreach (var (threadDoc, threadCountsDocument) in sorted)
            {
                var thread = ThreadModel.From(threadDoc);
                thread.CommentCount = threadCountsDocument.CommentCount;
                thread.ViewCount = threadCountsDocument.ViewCount;
                thread.LikeCount = threadCountsDocument.LikeCount;
                postsModels.Add(thread);
            }
            
            return postsModels;
        }
        catch (CosmosException e)
        {
            throw new ContentException(ContentError.UnexpectedError, e);
        }
    }
    
    private static IReadOnlyList<Comment> BuildCommentList(List<CommentDocument> comments, List<CommentCountsDocument>? commentCounts)
    {
        var sorted = comments
            .Join(commentCounts, i => i.CommentId, o => o.CommentId, (i, o) => (i, o))
            .OrderBy(x => x.i.Sk);

        var commentModels = new List<Comment>(comments.Count);
        foreach (var (commentDoc, countsDoc) in sorted)
        {
            var comment = Comment.From(commentDoc);
            Comment.Apply(comment, countsDoc);
            commentModels.Add(comment);
        }

        return commentModels;
    }
    
    private static ThreadModel? BuildPostModel(ThreadDocument thread, ThreadCountsDocument threadCounts, List<CommentDocument>? comments, List<CommentCountsDocument>? commentCounts)
    {
        var model = ThreadModel.From(thread);
        model.CommentCount = threadCounts.CommentCount;
        model.ViewCount = threadCounts.ViewCount +1;
        model.LikeCount = threadCounts.LikeCount;
        
        if (comments != null)
        {
            if (commentCounts == null)
                throw new InvalidOperationException($"Comments of post {thread.UserId}/{thread.ThreadId} are present but comment counts is null.");

            var sorted = comments
                .Join(commentCounts, i => i.CommentId, o => o.CommentId, (i, o) => (i, o))
                .OrderBy(x => x.i.Sk);
            
            foreach (var (commentDocument, commentCountsDocument) in sorted)
            {
                var comment = Comment.From(commentDocument);
                if (comment.CommentId != commentCountsDocument.CommentId)
                    throw new InvalidOperationException($"The comment {commentDocument.CommentId} does not match the counts.");
                
                Comment.Apply(comment, commentCountsDocument);
                model.LastComments.Add(comment);
            }
        }

        return model;
    }
}