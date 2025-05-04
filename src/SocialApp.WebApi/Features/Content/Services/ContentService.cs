using Microsoft.Azure.Cosmos;
using SocialApp.Models.Content;
using SocialApp.WebApi.Data.User;
using SocialApp.WebApi.Features._Shared.Services;
using SocialApp.WebApi.Features.Account.Services;
using SocialApp.WebApi.Features.Content.Containers;
using SocialApp.WebApi.Features.Content.Exceptions;
using SocialApp.WebApi.Features.Session.Models;

namespace SocialApp.WebApi.Features.Content.Services;

public interface IContentService
{
    Task<string> StartConversationAsync(UserSession user, string content, OperationContext context);
    Task<string> CommentAsync(UserSession user, string handle, string conversationId, string content, OperationContext context);
    Task UpdateConversationAsync(UserSession user, string conversationId, string content, OperationContext context);
    Task ReactToConversationAsync(UserSession user, string handle, string conversationId, bool like, OperationContext context);
    Task DeleteConversationAsync(UserSession user, string conversationId, OperationContext context);
    Task<Conversation> GetConversationAsync(string handle, string conversationId, int lastCommentCount, OperationContext context);
    Task<IReadOnlyList<ConversationComment>> GetPreviousCommentsAsync(string handle, string conversationId, string commentId, int lastCommentCount, OperationContext context);
    Task<IReadOnlyList<ConversationRoot>> GetUserConversationsAsync(string handle, string? beforeConversationId, int limit, OperationContext context);
}

public sealed class ContentService : IContentService
{
    private readonly UserDatabase _userDb;
    private readonly IUserHandleService _userHandleService;
    public ContentService(UserDatabase userDb, IUserHandleService userHandleService)
    {
        _userDb = userDb;
        _userHandleService = userHandleService;
    }

    private ContentContainer GetContentsContainer()
        => new(_userDb);
    
    public async Task<string> StartConversationAsync(UserSession user, string content, OperationContext context)
    {
        try
        {
            var conversationId = Ulid.NewUlid(context.UtcNow).ToString();
            var contents = GetContentsContainer();
            var conversation = new ConversationDocument(user.UserId, conversationId, content, context.UtcNow.UtcDateTime, 0, null, null) { IsRootConversation = true };

            await CreateConversationAsync(contents, conversation, context);
            return conversationId;
        }
        catch (Exception e)
        {
            throw new ContentException(ContentError.UnexpectedError, e);
        }
    }

    private static async Task CreateConversationAsync(ContentContainer contents, ConversationDocument conversation, OperationContext context)
    {
        var uow = contents.CreateUnitOfWork(conversation.Pk);
        uow.Create(conversation);
        uow.Create(conversation.CreateCounts());
        await uow.SaveChangesAsync(context);
    }

    public async Task<string> CommentAsync(UserSession user, string handle, string conversationId, string content, OperationContext context)
    {
        try
        {
            var commentId = Ulid.NewUlid(context.UtcNow).ToString();
            var contents = GetContentsContainer();

            var conversationUserId = await _userHandleService.GetUserIdAsync(handle, context);
            
            var comment = new CommentDocument(conversationUserId, conversationId, user.UserId, commentId, content, context.UtcNow.UtcDateTime, 0);
            var commentConversationKey = ConversationCountsDocument.Key(comment.ConversationUserId, comment.ConversationId);
            
            context.Signal("create-comment");
            var uow = contents.CreateUnitOfWork(comment.Pk);
            uow.Create(comment);
            uow.Create(comment.CreateCounts());
            uow.Increment<ConversationCountsDocument>(commentConversationKey, c => c.CommentCount);
            await uow.SaveChangesAsync(context);
            
            try
            {
                var conversation = new ConversationDocument(user.UserId, commentId, content, context.UtcNow.UtcDateTime, 0, conversationUserId, conversationId);
                context.Signal("create-comment-conversation");
                await CreateConversationAsync(contents, conversation, context);
            }
            catch (Exception e)
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

    public async Task UpdateConversationAsync(UserSession user, string conversationId, string content, OperationContext context)
    {
        try
        {
            var contents = GetContentsContainer();

            var key = ConversationDocument.Key(user.UserId, conversationId);
            var conversation = await contents.GetAsync<ConversationDocument>(key, context);
            
            if(conversation == null)
                throw new ContentException(ContentError.ContentNotFound);
            
            conversation = conversation with { Content = content, Version = conversation.Version + 1 };
            
            context.Signal("update-conversation");
            await contents.UpdateAsync(conversation, context);

            if (!string.IsNullOrWhiteSpace(conversation.ParentConversationUserId))
            {
                try
                {
                    context.Signal("update-comment");
                    var comment = new CommentDocument(conversation.ParentConversationUserId, conversation.ParentConversationId, conversation.UserId, conversation.ConversationId, conversation.Content, conversation.LastModify, conversation.Version);
                    await contents.UpdateAsync(comment, context);
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
    
    public async Task ReactToConversationAsync(UserSession user, string handle, string conversationId, bool like, OperationContext context)
    {
        try
        {
            var contents = GetContentsContainer();

            var conversationUserId = await _userHandleService.GetUserIdAsync(handle, context);
            var key = ConversationDocument.Key(conversationUserId, conversationId);
            var conversation = await contents.GetAsync<ConversationDocument>(key, context);
            
            if(conversation == null)
                throw new ContentException(ContentError.ContentNotFound);
            
            var reactionKey = ConversationUserLikeDocument.Key(user.UserId, conversationUserId, conversationId);
            var userReaction = await contents.GetAsync<ConversationUserLikeDocument>(reactionKey, context);

            if(userReaction != null && userReaction.Like == like)
                return;
            
            if(userReaction == null && !like)
                return;
            
            context.Signal("user-react-conversation");
            userReaction = new ConversationUserLikeDocument(user.UserId, conversationUserId, conversationId, like, conversation.ParentConversationUserId, conversation.ParentConversationId)
            {
                Ttl = like ? -1 : (int)TimeSpan.FromDays(2).TotalSeconds
            };
            await contents.CreateOrUpdateAsync(userReaction, context);

            try
            {
                context.Signal("react-conversation");
                var conversationReaction = new ConversationLikeDocument(conversationUserId, conversationId, user.UserId, like)
                {
                    Ttl = like ? -1 : (int)TimeSpan.FromDays(2).TotalSeconds
                };
                var countKey = ConversationCountsDocument.Key(conversationReaction.ConversationUserId, conversationReaction.ConversationId);
                
                var uow = contents.CreateUnitOfWork(conversationReaction.Pk);
                uow.CreateOrUpdate(conversationReaction);
                uow.Increment<ConversationCountsDocument>(countKey, c => c.LikeCount, conversationReaction.Like ? 1 : -1 );
                await uow.SaveChangesAsync(context);

                if (!string.IsNullOrWhiteSpace(conversation.ParentConversationUserId))
                {
                    context.Signal("react-comment");
                    var commentReaction = new CommentLikeDocument(conversation.ParentConversationUserId, conversation.ParentConversationId, conversation.ConversationId, user.UserId, like)
                    {
                        Ttl = like ? -1 : (int)TimeSpan.FromDays(2).TotalSeconds
                    };
                    countKey = CommentCountsDocument.Key(commentReaction.ConversationUserId, commentReaction.ConversationId, commentReaction.CommentId);
                    
                    uow = contents.CreateUnitOfWork(commentReaction.Pk);
                    uow.CreateOrUpdate(commentReaction);
                    uow.Increment<CommentCountsDocument>(countKey, cc => cc.LikeCount, commentReaction.Like ? 1 : -1 );
                    await uow.SaveChangesAsync(context);
                }
            }
            catch (CosmosException e)
            {
                // Change Feed will fix this
                // Log ?
            }
        }
        catch (CosmosException e)
        {
            throw new ContentException(ContentError.UnexpectedError, e);
        }
    }
    
    public async Task DeleteConversationAsync(UserSession user, string conversationId, OperationContext context)
    {
        try
        {
            var contents = GetContentsContainer();
        
            var key = ConversationDocument.Key(user.UserId, conversationId);
            var conversation = await contents.GetAsync<ConversationDocument>(key, context);
        
            context.Signal("delete-conversation");

            var uow = contents.CreateUnitOfWork(conversation.Pk);
            uow.Set<ConversationDocument>(key, c => c.Deleted, true);
            uow.Set<ConversationDocument>(key, c => c.Ttl, TimeSpan.FromDays(1).TotalSeconds );
            uow.Set<ConversationCountsDocument>(key, c => c.Deleted, true );
            uow.Set<ConversationCountsDocument>(key, c => c.Ttl, TimeSpan.FromDays(1).TotalSeconds );
            await uow.SaveChangesAsync(context);

            if (!string.IsNullOrWhiteSpace(conversation.ParentConversationUserId))
            {
                try
                {
                    context.Signal("delete-comment");
                    var commentKey = CommentDocument.Key(conversation.ParentConversationUserId, conversation.ParentConversationId, conversation.ConversationId);

                    uow = contents.CreateUnitOfWork(commentKey.Pk);
                    uow.Set<CommentDocument>(commentKey, c => c.Deleted, true );
                    uow.Set<CommentDocument>(commentKey, c => c.Ttl, TimeSpan.FromDays(1).TotalSeconds );
                    uow.Set<CommentCountsDocument>(commentKey, c => c.Deleted, true );
                    uow.Set<CommentCountsDocument>(commentKey, c => c.Ttl, TimeSpan.FromDays(1).TotalSeconds );
                    var parentKey = ConversationCountsDocument.Key(conversation.ParentConversationUserId, conversation.ParentConversationId);
                    uow.Increment<ConversationCountsDocument>(parentKey, c => c.CommentCount, -1);
                    await uow.SaveChangesAsync(context);
                }
                catch (Exception)
                {
                    // Change feed will correct this.
                    // log?
                }
            }
        }
        catch (Exception e)
        {
            throw new ContentException(ContentError.UnexpectedError, e);
        }
    }
    
    public async Task<Conversation> GetConversationAsync(string handle, string conversationId, int lastCommentCount, OperationContext context)
    {
        var contents = GetContentsContainer();
        try
        {
            var conversationUserId = await _userHandleService.GetUserIdAsync(handle, context);
            var documents = await contents.GetAllConversationDocumentsAsync(conversationUserId, conversationId, lastCommentCount, context);
            if(documents.Conversation == null)
                throw new ContentException(ContentError.ContentNotFound);

            
            var keyFrom = ConversationCountsDocument.Key(conversationUserId, conversationId);
            var uow = contents.CreateUnitOfWork(keyFrom.Pk);
            uow.Increment<ConversationCountsDocument>(keyFrom, c => c.ViewCount);
            await uow.SaveChangesAsync(context);
            
            return await BuildConversationModelAsync(documents.Conversation, documents.ConversationCounts, documents.Comments, documents.CommentCounts, context);
        }
        catch (CosmosException e)
        {
            throw new ContentException(ContentError.UnexpectedError, e);
        }
    }
    
    public async Task<IReadOnlyList<ConversationComment>> GetPreviousCommentsAsync(string handle, string conversationId, string commentId, int lastCommentCount, OperationContext context)
    {
        try
        {
            var contents = GetContentsContainer();
            var conversationUserId = await _userHandleService.GetUserIdAsync(handle, context);
            var (comments, commentCounts) = await contents.GetPreviousCommentsAsync(conversationUserId, conversationId, commentId, lastCommentCount, context);
            if (comments == null)
                return Array.Empty<ConversationComment>();

            return await BuildCommentListAsync(comments, commentCounts, context);
        }
        catch (CosmosException e)
        {
            throw new ContentException(ContentError.UnexpectedError, e);
        }
    }
    
    public async Task<IReadOnlyList<ConversationRoot>> GetUserConversationsAsync(string handle, string? beforeConversationId, int limit, OperationContext context)
    {
        var contents = GetContentsContainer();

        try
        {
            var conversationUserId = await _userHandleService.GetUserIdAsync(handle, context);
            var (conversations, conversationCounts) = await contents.GetUserConversationsDocumentsAsync(conversationUserId, beforeConversationId, limit, context);
            if (conversations == null || conversations.Count == 0)
                return Array.Empty<ConversationRoot>();
            
            var sorted = conversations
                .Join(conversationCounts, i => i.ConversationId, o => o.ConversationId, (i, o) => (i, o))
                .OrderByDescending(x => x.i.Sk);

            var conversationsModels = new List<ConversationRoot>(conversations.Count);
            foreach (var (conversationDoc, conversationCountsDocument) in sorted)
            {
                var conversation = await BuildConversationAsync(conversationDoc, conversationCountsDocument, context);
                conversationsModels.Add(conversation.Root);
            }
            
            return conversationsModels;
        }
        catch (CosmosException e)
        {
            throw new ContentException(ContentError.UnexpectedError, e);
        }
    }
    
    private async Task<IReadOnlyList<ConversationComment>> BuildCommentListAsync(List<CommentDocument> comments, List<CommentCountsDocument>? commentCounts, OperationContext context)
    {
        var sorted = comments
            .Join(commentCounts, i => i.CommentId, o => o.CommentId, (i, o) => (i, o))
            .OrderBy(x => x.i.Sk);

        var commentModels = new List<ConversationComment>(comments.Count);
        foreach (var (commentDoc, countsDoc) in sorted)
        {
            var comment = await BuildCommentAsync(commentDoc, countsDoc, context);
            commentModels.Add(comment);
        }

        return commentModels;
    }
    
    private async Task<Conversation> BuildConversationModelAsync(ConversationDocument conversation, ConversationCountsDocument conversationCounts, List<CommentDocument>? comments, List<CommentCountsDocument>? commentCounts, OperationContext context)
    {
        var model = await BuildConversationAsync(conversation, conversationCounts, context);
        model.Root.ViewCount++;
        
        if (comments != null)
        {
            if (commentCounts == null)
                throw new InvalidOperationException($"Comments of conversation {conversation.UserId}/{conversation.ConversationId} are present but comment counts is null.");

            var sorted = comments
                .Join(commentCounts, i => i.CommentId, o => o.CommentId, (i, o) => (i, o))
                .OrderBy(x => x.i.Sk);
            
            foreach (var (commentDocument, commentCountsDocument) in sorted)
            {
                if (commentDocument.CommentId != commentCountsDocument.CommentId)
                    throw new InvalidOperationException($"The comment {commentDocument.CommentId} does not match the counts.");
                
                var comment = await BuildCommentAsync(commentDocument, commentCountsDocument, context);
                model.LastComments.Add(comment);
            }
        }

        return model;
    }
    
    private async Task<ConversationComment> BuildCommentAsync(CommentDocument comment, CommentCountsDocument counts, OperationContext context)
        => new()
        {
            Handle = await _userHandleService.GetHandleAsync(comment.UserId, context),
            CommentId = comment.CommentId,
            Content = comment.Content,
            LastModify = comment.LastModify,
            ViewCount = counts.ViewCount,
            CommentCount = counts.CommentCount,
            LikeCount = counts.LikeCount
        };

    private async Task<Conversation> BuildConversationAsync(ConversationDocument conversation, ConversationCountsDocument counts, OperationContext context)
        => new()
        {
            Root = new ConversationRoot
            {
                Handle = await _userHandleService.GetHandleAsync(conversation.UserId, context),
                ConversationId = conversation.ConversationId,
                Content = conversation.Content,
                LastModify = conversation.LastModify,
                CommentCount = counts.CommentCount,
                ViewCount = counts.ViewCount,
                LikeCount = counts.LikeCount,
            },
            LastComments = new List<ConversationComment>()
        };
}