using System.Net;
using Microsoft.Azure.Cosmos;
using SocialApp.WebApi.Data._Shared;
using SocialApp.WebApi.Data.User;
using SocialApp.WebApi.Features._Shared.Services;
using SocialApp.WebApi.Features.Content.Exceptions;

namespace SocialApp.WebApi.Features.Content.Containers;

public record struct AllConversationDocuments(ConversationDocument? Conversation, ConversationCountsDocument? ConversationCounts, List<CommentDocument>? Comments, List<CommentCountsDocument>? CommentCounts);

public sealed class ContentContainer : CosmoContainer
{
    private static readonly PatchItemRequestOptions _patchItemNoResponse = new() { EnableContentResponseOnWrite = false};
    
    public ContentContainer(UserDatabase database)
        :base(database, "contents") { }
    
    public async Task<(IReadOnlyList<ConversationDocument>, IReadOnlyList<ConversationCountsDocument>)> GetUserConversationsDocumentsAsync(string userId, string? beforeConversationId, int limit, OperationContext context)
    {
        var key = ConversationDocument.KeyUserConversationsEnd(userId);

        const string sql = @"
            select * 
            from c 
            where c.pk = @pk 
              and c.isRootConversation = true
              and c.sk < @id 
              and not is_defined(c.parentConversationUserId)
              and not is_defined(c.deleted)
            order by c.sk desc 
            offset 0 limit @limit";
        
        var query = new QueryDefinition(sql)
            .WithParameter("@pk", key.Pk)
            .WithParameter("@id", beforeConversationId == null ? key.Id : ConversationDocument.Key(userId, beforeConversationId).Id)
            .WithParameter("@limit", limit * 2);
        
        var conversations = new List<ConversationDocument>();
        var conversationCounts = new List<ConversationCountsDocument>();
        await foreach (var document in ExecuteQueryReaderAsync(query, key.Pk, context))
        {
            if(document is ConversationDocument conversationDocument)
                conversations.Add(conversationDocument);
            else if (document is ConversationCountsDocument conversationCountsDocument)
                conversationCounts.Add(conversationCountsDocument);
            else
                throw new InvalidOperationException("Unexpected document: " + document.GetType().Name);
        }

        return (conversations, conversationCounts);
    }
    
    public async Task<AllConversationDocuments> GetAllConversationDocumentsAsync(string userId, string conversationId, int lastCommentCount, OperationContext context)
    {
        var keyFrom = ConversationDocument.KeyConversationsItemsStart(userId, conversationId);
        var keyTo = ConversationDocument.KeyConversationItemsEnd(userId, conversationId);

        const string sql = @"
            select * from c 
            where c.pk = @pk 
              and c.sk >= @id 
              and c.sk < @id_end
              and not is_defined(c.deleted) 
            order by c.sk desc 
            offset 0 limit @limit";
        var query = new QueryDefinition(sql)
            .WithParameter("@pk", keyFrom.Pk)
            .WithParameter("@id", keyFrom.Id)
            .WithParameter("@id_end", keyTo.Id)
            .WithParameter("@limit", 2 + lastCommentCount * 2 );
        
        ConversationDocument? conversation = null;
        ConversationCountsDocument? conversationCounts = null;
        var comments = new List<CommentDocument>();
        var commentCounts = new List<CommentCountsDocument>();
        await foreach (var document in ExecuteQueryReaderAsync(query, keyFrom.Pk, context))
        {
            if(document is ConversationDocument conversationDocument)
                conversation = conversation == null ? conversationDocument : throw new InvalidOperationException("Expecting a single conversation.");
            else if(document is ConversationCountsDocument conversationCountsDocument)
                conversationCounts = conversationCounts == null ? conversationCountsDocument : throw new InvalidOperationException("Expecting a single conversation.");
            else if(document is CommentDocument commentDocument)
                comments.Add(commentDocument);
            else if(document is CommentCountsDocument commentCountsDocument)
                commentCounts.Add(commentCountsDocument);
            else
                throw new InvalidOperationException("Unexpected document: " + document.GetType().Name);
        }
        
        if(conversation == null || conversation is { Deleted: true })
            return new AllConversationDocuments(null, null, null, null);
        
        return new AllConversationDocuments(conversation, conversationCounts, comments, commentCounts);
    }
    
    public async Task<(List<CommentDocument>? comments, List<CommentCountsDocument>? commentCounts)> GetPreviousCommentsAsync(string userId, string conversationId, string commentId, int lastCommentCount, OperationContext context)
    {
        var key = CommentDocument.Key(userId, conversationId, commentId);
        var keyTo = ConversationDocument.KeyConversationsItemsStart(userId, conversationId);

        const string sql = @"
            select * from c 
               where c.pk = @pk 
                and c.sk < @id 
                and c.sk > @id_end 
                and not is_defined(c.deleted)
              order by c.sk desc offset 0 limit @limit";
        
        var query = new QueryDefinition(sql)
            .WithParameter("@pk", key.Pk)
            .WithParameter("@id", key.Id)
            .WithParameter("@id_end", keyTo.Id)
            .WithParameter("@limit", lastCommentCount * 2);
        
        var comments = new List<CommentDocument>();
        var commentCounts = new List<CommentCountsDocument>();
        await foreach (var document in ExecuteQueryReaderAsync(query, key.Pk, context))
        {
            if(document is CommentDocument commentDocument)
                comments.Add(commentDocument);
            else if(document is CommentCountsDocument commentCountsDocument)
                commentCounts.Add(commentCountsDocument);
            else
                throw new InvalidOperationException("Unexpected document: " + document.GetType().Name);
        }
        
        return (comments, commentCounts);
    }
    
    public async Task IncreaseViewsAsync(string userId, string conversationId, OperationContext context)
    {
        // TODO: Defer
        // Increase views
        var keyFrom = ConversationCountsDocument.Key(userId, conversationId);
        var response = await Container.PatchItemAsync<ConversationDocument>
        (
            keyFrom.Id,
            new PartitionKey(keyFrom.Pk),
            [PatchOperation.Increment("/viewCount", 1)],
            _patchItemNoResponse, 
            context.Cancellation
        );
        context.AddRequestCharge(response.RequestCharge);
    }
    
}