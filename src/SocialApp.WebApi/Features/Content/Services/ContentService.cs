using Microsoft.Azure.Cosmos;
using SocialApp.WebApi.Data.User;
using SocialApp.WebApi.Features._Shared.Services;
using SocialApp.WebApi.Features.Content.Containers;
using SocialApp.WebApi.Features.Content.Exceptions;
using SocialApp.WebApi.Features.Content.Models;
using SocialApp.WebApi.Features.Session.Models;

namespace SocialApp.WebApi.Features.Content.Services;

public interface IContentService
{
    Task<string> StartConversationAsync(UserSession user, string content, OperationContext context);
    Task<string> CommentAsync(UserSession user, string conversationUserId, string conversationId, string content, OperationContext context);
    Task UpdateConversationAsync(UserSession user, string conversationId, string content, OperationContext context);
    Task ReactToConversationAsync(UserSession user, string conversationUserId, string conversationId, bool like, OperationContext context);
    Task DeleteConversationAsync(UserSession user, string conversationId, OperationContext context);
    Task<ConversationModel> GetConversationAsync(UserSession user, string userId, string conversationId, int lastCommentCount, OperationContext context);
    Task<IReadOnlyList<CommentModel>> GetPreviousCommentsAsync(UserSession user, string conversationUserId, string conversationId, string commentId, int lastCommentCount, OperationContext context);
    Task<IReadOnlyList<ConversationModel>> GetUserConversationsAsync(UserSession user, string? afterConversationId, int limit, OperationContext context);
}

public sealed class ContentService : IContentService
{
    private readonly UserDatabase _userDb;
    public ContentService(UserDatabase userDb)
        => _userDb = userDb;

    private ContentContainer GetContentsContainer()
        => new(_userDb);
    
    public async Task<string> StartConversationAsync(UserSession user, string content, OperationContext context)
    {
        try
        {
            var conversationId = Ulid.NewUlid(context.UtcNow).ToString();
            var contents = GetContentsContainer();
            var conversation = new ConversationDocument(user.UserId, conversationId, content, context.UtcNow.UtcDateTime, 0, null, null) { IsRootConversation = true };
            await contents.CreateConversationAsync(conversation, context);
            return conversationId;
        }
        catch (CosmosException e)
        {
            throw new ContentException(ContentError.UnexpectedError, e);
        }
    }
    
    public async Task<string> CommentAsync(UserSession user, string conversationUserId, string conversationId, string content, OperationContext context)
    {
        try
        {
            var commentId = Ulid.NewUlid(context.UtcNow).ToString();
            var contents = GetContentsContainer();
            
            var comment = new CommentDocument(conversationUserId, conversationId, user.UserId, commentId, content, context.UtcNow.UtcDateTime, 0);
            var conversation = new ConversationDocument(user.UserId, commentId, content, context.UtcNow.UtcDateTime, 0, conversationUserId, conversationId);
            
            context.Signal("create-comment");
            await contents.CreateCommentAsync(comment, context);

            try
            {
                context.Signal("create-comment-conversation");
                await contents.CreateConversationAsync(conversation, context);
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

    public async Task UpdateConversationAsync(UserSession user, string conversationId, string content, OperationContext context)
    {
        try
        {
            var contents = GetContentsContainer();

            var conversation = await contents.GetConversationDocumentAsync(user.UserId, conversationId, context);
            
            if(conversation == null)
                throw new ContentException(ContentError.ContentNotFound);
            
            conversation = conversation with { Content = content, Version = conversation.Version + 1 };
            
            context.Signal("update-conversation");
            await contents.ReplaceDocumentAsync(conversation, context);

            if (!string.IsNullOrWhiteSpace(conversation.ParentConversationUserId))
            {
                try
                {
                    context.Signal("update-comment");
                    var comment = new CommentDocument(conversation.ParentConversationUserId, conversation.ParentConversationId, conversation.UserId, conversation.ConversationId, conversation.Content, conversation.LastModify, conversation.Version);
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
    
    public async Task ReactToConversationAsync(UserSession user, string conversationUserId, string conversationId, bool like, OperationContext context)
    {
        try
        {
            var contents = GetContentsContainer();

            var conversation = await contents.GetConversationDocumentAsync(conversationUserId, conversationId, context);
            
            if(conversation == null)
                throw new ContentException(ContentError.ContentNotFound);
            
            var userReaction = await contents.GetUserConversationLikeAsync(user.UserId, conversationUserId, conversationId, context);
            
            if(userReaction != null && userReaction.Like == like)
                return;
            
            if(userReaction == null && !like)
                return;
            
            context.Signal("user-react-conversation");
            userReaction = new ConversationUserLikeDocument(user.UserId, conversationUserId, conversationId, like, conversation.ParentConversationUserId, conversation.ParentConversationId)
            {
                Ttl = like ? -1 : (int)TimeSpan.FromDays(2).TotalSeconds
            };
            await contents.UserReactConversationAsync(userReaction, context);

            try
            {
                context.Signal("react-conversation");
                var conversationReaction = new ConversationLikeDocument(conversationUserId, conversationId, user.UserId, like)
                {
                    Ttl = like ? -1 : (int)TimeSpan.FromDays(2).TotalSeconds
                };
                await contents.ReactConversationAsync(conversationReaction, context);

                if (!string.IsNullOrWhiteSpace(conversation.ParentConversationUserId))
                {
                    context.Signal("react-comment");
                    var commentReaction = new CommentLikeDocument(conversation.ParentConversationUserId, conversation.ParentConversationId, conversation.ConversationId, user.UserId, like)
                    {
                        Ttl = like ? -1 : (int)TimeSpan.FromDays(2).TotalSeconds
                    };
                    await contents.ReactCommentAsync(commentReaction, context);
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
        
            var conversation = await contents.GetConversationDocumentAsync(user.UserId, conversationId, context);
        
            context.Signal("delete-conversation");
            await contents.RemoveConversationAsync(conversation, context);

            if (!string.IsNullOrWhiteSpace(conversation.ParentConversationUserId))
            {
                try
                {
                    context.Signal("delete-comment");
                    await contents.RemoveCommentAsync(conversation.ParentConversationUserId, conversation.ParentConversationId, conversation.ConversationId, context);
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
    
    public async Task<ConversationModel> GetConversationAsync(UserSession user, string userId, string conversationId, int lastCommentCount, OperationContext context)
    {
        var contents = GetContentsContainer();
        try
        {
            var documents = await contents.GetAllConversationDocumentsAsync(userId, conversationId, lastCommentCount, context);
            if(documents.Conversation == null)
                throw new ContentException(ContentError.ContentNotFound);

            await contents.IncreaseViewsAsync(user.UserId, conversationId, context);
            return BuildConversationModel(documents.Conversation, documents.ConversationCounts, documents.Comments, documents.CommentCounts);
        }
        catch (CosmosException e)
        {
            throw new ContentException(ContentError.UnexpectedError, e);
        }
    }
    
    public async Task<IReadOnlyList<CommentModel>> GetPreviousCommentsAsync(UserSession user, string conversationUserId, string conversationId, string commentId, int lastCommentCount, OperationContext context)
    {
        try
        {
            var contents = GetContentsContainer();
            var (comments, commentCounts) = await contents.GetPreviousCommentsAsync(conversationUserId, conversationId, commentId, lastCommentCount, context);
            if (comments == null)
                return Array.Empty<CommentModel>();

            return BuildCommentList(comments, commentCounts);
        }
        catch (CosmosException e)
        {
            throw new ContentException(ContentError.UnexpectedError, e);
        }
    }
    
    public async Task<IReadOnlyList<ConversationModel>> GetUserConversationsAsync(UserSession user, string? afterConversationId, int limit, OperationContext context)
    {
        var contents = GetContentsContainer();

        try
        {
            var (conversations, conversationCounts) = await contents.GetUserConversationsDocumentsAsync(user.UserId, afterConversationId, limit, context);
            if (conversations == null || conversations.Count == 0)
                return Array.Empty<ConversationModel>();
            
            var sorted = conversations
                .Join(conversationCounts, i => i.ConversationId, o => o.ConversationId, (i, o) => (i, o))
                .OrderByDescending(x => x.i.Sk);

            var conversationsModels = new List<ConversationModel>(conversations.Count);
            foreach (var (conversationDoc, conversationCountsDocument) in sorted)
            {
                var conversation = ConversationModel.From(conversationDoc);
                conversation.CommentCount = conversationCountsDocument.CommentCount;
                conversation.ViewCount = conversationCountsDocument.ViewCount;
                conversation.LikeCount = conversationCountsDocument.LikeCount;
                conversationsModels.Add(conversation);
            }
            
            return conversationsModels;
        }
        catch (CosmosException e)
        {
            throw new ContentException(ContentError.UnexpectedError, e);
        }
    }
    
    private static IReadOnlyList<CommentModel> BuildCommentList(List<CommentDocument> comments, List<CommentCountsDocument>? commentCounts)
    {
        var sorted = comments
            .Join(commentCounts, i => i.CommentId, o => o.CommentId, (i, o) => (i, o))
            .OrderBy(x => x.i.Sk);

        var commentModels = new List<CommentModel>(comments.Count);
        foreach (var (commentDoc, countsDoc) in sorted)
        {
            var comment = CommentModel.From(commentDoc);
            CommentModel.Apply(comment, countsDoc);
            commentModels.Add(comment);
        }

        return commentModels;
    }
    
    private static ConversationModel? BuildConversationModel(ConversationDocument conversation, ConversationCountsDocument conversationCounts, List<CommentDocument>? comments, List<CommentCountsDocument>? commentCounts)
    {
        var model = ConversationModel.From(conversation);
        model.CommentCount = conversationCounts.CommentCount;
        model.ViewCount = conversationCounts.ViewCount +1;
        model.LikeCount = conversationCounts.LikeCount;
        
        if (comments != null)
        {
            if (commentCounts == null)
                throw new InvalidOperationException($"Comments of conversation {conversation.UserId}/{conversation.ConversationId} are present but comment counts is null.");

            var sorted = comments
                .Join(commentCounts, i => i.CommentId, o => o.CommentId, (i, o) => (i, o))
                .OrderBy(x => x.i.Sk);
            
            foreach (var (commentDocument, commentCountsDocument) in sorted)
            {
                var comment = CommentModel.From(commentDocument);
                if (comment.CommentId != commentCountsDocument.CommentId)
                    throw new InvalidOperationException($"The comment {commentDocument.CommentId} does not match the counts.");
                
                CommentModel.Apply(comment, commentCountsDocument);
                model.LastComments.Add(comment);
            }
        }

        return model;
    }
}