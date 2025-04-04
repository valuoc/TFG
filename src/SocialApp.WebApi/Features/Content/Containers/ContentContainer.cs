using System.Net;
using Microsoft.Azure.Cosmos;
using SocialApp.WebApi.Data.Shared;
using SocialApp.WebApi.Data.User;
using SocialApp.WebApi.Features._Shared.Services;
using SocialApp.WebApi.Features.Content.Exceptions;

namespace SocialApp.WebApi.Features.Content.Containers;

public record struct AllConversationDocuments(ConversationDocument? Conversation, ConversationCountsDocument? ConversationCounts, List<CommentDocument>? Comments, List<CommentCountsDocument>? CommentCounts);

public sealed class ContentContainer : CosmoContainer
{
    private static readonly TransactionalBatchPatchItemRequestOptions _noBatchResponse = new() {EnableContentResponseOnWrite = false};
    private static readonly ItemRequestOptions _noResponseContent = new(){ EnableContentResponseOnWrite = false};
    private static readonly TransactionalBatchItemRequestOptions _noResponse = new() { EnableContentResponseOnWrite = false };
    private static readonly PatchItemRequestOptions _patchItemNoResponse = new() { EnableContentResponseOnWrite = false};
    
    private static readonly double _secondsInADay = TimeSpan.FromDays(1).TotalSeconds;
    
    public ContentContainer(UserDatabase database)
        :base(database) { }
    
    public async Task<AllConversationDocuments> CreateConversationAsync(ConversationDocument conversation, OperationContext context)
    {
        var conversationCounts = new ConversationCountsDocument(conversation.UserId, conversation.ConversationId, 0, 0, 0, conversation.ParentConversationUserId, conversation.ParentConversationId)
        {
            IsRootConversation = conversation.IsRootConversation
        };
        
        var batch = Container.CreateTransactionalBatch(new PartitionKey(conversation.Pk));
        batch.CreateItem(conversation, _noResponse);
        batch.CreateItem(conversationCounts, _noResponse);
        
        var response = await batch.ExecuteAsync(context.Cancellation);
        context.AddRequestCharge(response.RequestCharge);
        
        ThrowErrorIfTransactionFailed(ContentError.TransactionFailed, response);
        return new AllConversationDocuments(conversation, conversationCounts, null, null);
    }
    
    public async Task<(IReadOnlyList<ConversationDocument>, IReadOnlyList<ConversationCountsDocument>)> GetUserConversationsDocumentsAsync(string userId, string? afterConversationId, int limit, OperationContext context)
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
            .WithParameter("@id", afterConversationId == null ? key.Id : ConversationDocument.Key(userId, afterConversationId).Id)
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
    
    public async Task CreateCommentAsync(CommentDocument comment, OperationContext context)
    {
        var commentCounts = new CommentCountsDocument(comment.ConversationUserId, comment.ConversationId, comment.UserId, comment.CommentId, 0, 0, 0);
        var batch = Container.CreateTransactionalBatch(new PartitionKey(comment.Pk));
        batch.CreateItem(comment, _noResponse);
        batch.CreateItem(commentCounts, _noResponse);
        batch.PatchItem(ConversationCountsDocument.Key(comment.ConversationUserId, comment.ConversationId).Id, [PatchOperation.Increment( "/commentCount", 1)], _noBatchResponse);
        
        var response = await batch.ExecuteAsync(context.Cancellation);
        context.AddRequestCharge(response.RequestCharge);
        ThrowErrorIfTransactionFailed(ContentError.TransactionFailed, response);
    }
    
    public async Task<CommentDocument?> GetCommentAsync(string userId, string conversationId, string commentId, OperationContext context)
    {
        try
        {
            var key = CommentDocument.Key(userId, conversationId, commentId);
            var response = await Container.ReadItemAsync<CommentDocument>(key.Id, new PartitionKey(key.Pk), _noResponseContent, context.Cancellation);
            context.AddRequestCharge(response.RequestCharge);
            if(response.Resource is { Deleted: false })
                return response.Resource;
        }
        catch (CosmosException e) when (e.StatusCode == HttpStatusCode.NotFound)
        {
            context.AddRequestCharge(e.RequestCharge);
        }
        return null;
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
    
    public async Task<ConversationDocument?> GetConversationDocumentAsync(string userId, string conversationId, OperationContext context)
    {
        try
        {
            var key = ConversationDocument.Key(userId, conversationId);
            var response = await Container.ReadItemAsync<ConversationDocument>(key.Id, new PartitionKey(key.Pk), _noResponseContent, context.Cancellation);
            context.AddRequestCharge(response.RequestCharge);
            if (response.Resource is { Deleted: false })
                return response.Resource;
        }
        catch (CosmosException e) when (e.StatusCode == HttpStatusCode.NotFound)
        {
            context.AddRequestCharge(e.RequestCharge);
        }
        return null;
    }
    
    public async Task ReplaceDocumentAsync<T>(T document, OperationContext context)
        where T : Document
    {
        var response = await Container.ReplaceItemAsync
        (
            document,
            document.Id, new PartitionKey(document.Pk),
            new ItemRequestOptions { IfMatchEtag = document.ETag, EnableContentResponseOnWrite = false },
            context.Cancellation
        );
        context.AddRequestCharge(response.RequestCharge);
    }
    
    public async Task RemoveConversationAsync(ConversationDocument document, OperationContext context)
    {
        var batch = Container.CreateTransactionalBatch(new PartitionKey(document.Pk));
        var deletePatch = new[] {PatchOperation.Set("/deleted", true), PatchOperation.Set("/ttl", _secondsInADay)};
        batch.PatchItem(document.Id, deletePatch, _noBatchResponse);
        batch.PatchItem(ConversationCountsDocument.Key(document.UserId, document.ConversationId).Id, deletePatch, _noBatchResponse);
        var response = await batch.ExecuteAsync(context.Cancellation);
        context.AddRequestCharge(response.RequestCharge);
        ThrowErrorIfTransactionFailed(ContentError.TransactionFailed, response);
    }
    
    public async Task RemoveCommentAsync(string parentConversationUserId, string parentConversationId, string commentId, OperationContext context)
    {
        var commentKey = CommentDocument.Key(parentConversationUserId, parentConversationId, commentId);
        var batch = Container.CreateTransactionalBatch(new PartitionKey(commentKey.Pk));
        var deletePatch = new[] {PatchOperation.Set("/deleted", true), PatchOperation.Set("/ttl", _secondsInADay)};
        batch.PatchItem(commentKey.Id, deletePatch, _noBatchResponse);
        batch.PatchItem(CommentCountsDocument.Key(parentConversationUserId, parentConversationId, commentId).Id, deletePatch, _noBatchResponse);
        batch.PatchItem(ConversationCountsDocument.Key(parentConversationUserId, parentConversationId).Id, [PatchOperation.Increment( "/commentCount", -1)], _noBatchResponse);
        var response = await batch.ExecuteAsync(context.Cancellation);
        context.AddRequestCharge(response.RequestCharge);
        ThrowErrorIfTransactionFailed(ContentError.TransactionFailed, response);
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
    
    private static void ThrowErrorIfTransactionFailed(ContentError error, TransactionalBatchResponse response)
    {
        if (!response.IsSuccessStatusCode)
        {
            for (var i = 0; i < response.Count; i++)
            {
                var sub = response[i];
                if (sub.StatusCode != HttpStatusCode.FailedDependency)
                    throw new ContentException(error, new CosmosException($"{error}. Batch failed at position [{i}]: {sub.StatusCode}. {response.ErrorMessage}", sub.StatusCode, 0, i.ToString(), 0));
            }
        }
    }

    public async Task UserReactConversationAsync(ConversationUserLikeDocument reaction, OperationContext context)
    {
        var batch = Container.CreateTransactionalBatch(new PartitionKey(reaction.Pk));
        batch.UpsertItem(reaction, new TransactionalBatchItemRequestOptions()
        {
            IfMatchEtag = reaction.ETag,
            EnableContentResponseOnWrite = false
        });
        
        var response = await batch.ExecuteAsync(context.Cancellation);
        context.AddRequestCharge(response.RequestCharge);
        ThrowErrorIfTransactionFailed(ContentError.TransactionFailed, response);
    }
    
    public async Task ReactConversationAsync(ConversationLikeDocument reaction, OperationContext context)
    {
        var batch = Container.CreateTransactionalBatch(new PartitionKey(reaction.Pk));
        batch.UpsertItem(reaction);
        
        var counts = ConversationCountsDocument.Key(reaction.ConversationUserId, reaction.ConversationId);
        var inc = new[] {PatchOperation.Increment("/likeCount", reaction.Like ? 1 : -1 )};
        batch.PatchItem(counts.Id, inc, _noBatchResponse);

        var response = await batch.ExecuteAsync(context.Cancellation);
        context.AddRequestCharge(response.RequestCharge);
        ThrowErrorIfTransactionFailed(ContentError.TransactionFailed, response);
    }
    
    public async Task ReactCommentAsync(CommentLikeDocument reaction, OperationContext context)
    {
        var batch = Container.CreateTransactionalBatch(new PartitionKey(reaction.Pk));
        batch.UpsertItem(reaction);
        
        var counts = CommentCountsDocument.Key(reaction.ConversationUserId, reaction.ConversationId, reaction.CommentId);
        var inc = new[] { PatchOperation.Increment("/likeCount", reaction.Like ? 1 : -1) };
        batch.PatchItem(counts.Id, inc, _noBatchResponse);

        var response = await batch.ExecuteAsync(context.Cancellation);
        context.AddRequestCharge(response.RequestCharge);
        ThrowErrorIfTransactionFailed(ContentError.TransactionFailed, response);
    }
    
    public async Task<ConversationUserLikeDocument?> GetUserConversationLikeAsync(string userId, string conversationUserId, string conversationId, OperationContext context)
    {
        var key = ConversationUserLikeDocument.Key(userId, conversationUserId, conversationId);
        return await TryGetAsync<ConversationUserLikeDocument>(key, context);
    }
    
    public async Task<ConversationLikeDocument?> GetConversationReactionAsync(string conversationUserId, string conversationId, string userId, OperationContext context)
    {
        var key = ConversationLikeDocument.Key(conversationUserId, conversationId, userId);
        return await TryGetAsync<ConversationLikeDocument>(key, context);
    }

    public async Task<CommentLikeDocument?> GetCommentReactionAsync(string conversationUserId, string conversationId, string commentId, string userId, OperationContext context)
    {
        var key = CommentLikeDocument.Key(conversationUserId, conversationId, commentId, userId);
        return await TryGetAsync<CommentLikeDocument>(key, context);
    }
}