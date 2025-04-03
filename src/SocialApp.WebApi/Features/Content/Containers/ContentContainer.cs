using System.Net;
using Microsoft.Azure.Cosmos;
using SocialApp.WebApi.Data._Shared;
using SocialApp.WebApi.Data.User;
using SocialApp.WebApi.Features._Shared.Services;
using SocialApp.WebApi.Features.Content.Exceptions;

namespace SocialApp.WebApi.Features.Content.Containers;

public record struct AllThreadDocuments(ThreadDocument? Thread, ThreadCountsDocument? ThreadCounts, List<CommentDocument>? Comments, List<CommentCountsDocument>? CommentCounts);

public sealed class ContentContainer : CosmoContainer
{
    private static readonly TransactionalBatchPatchItemRequestOptions _noPatchResponse = new() {EnableContentResponseOnWrite = false};
    private static readonly ItemRequestOptions _noResponseContent = new(){ EnableContentResponseOnWrite = false};
    private static readonly TransactionalBatchItemRequestOptions _noResponse = new() { EnableContentResponseOnWrite = false };
    private static readonly PatchItemRequestOptions _patchItemNoResponse = new() { EnableContentResponseOnWrite = false};
    
    private static readonly double _secondsInADay = TimeSpan.FromDays(1).TotalSeconds;
    
    public ContentContainer(UserDatabase database)
        :base(database) { }
    
    public async Task<AllThreadDocuments> CreateThreadAsync(ThreadDocument thread, OperationContext context)
    {
        var postCounts = new ThreadCountsDocument(thread.UserId, thread.ThreadId, 0, 0, 0, thread.ParentThreadUserId, thread.ParentThreadId)
        {
            IsRootThread = thread.IsRootThread
        };
        
        var batch = Container.CreateTransactionalBatch(new PartitionKey(thread.Pk));
        batch.CreateItem(thread, _noResponse);
        batch.CreateItem(postCounts, _noResponse);
        
        var response = await batch.ExecuteAsync(context.Cancellation);
        context.AddRequestCharge(response.RequestCharge);
        
        ThrowErrorIfTransactionFailed(ContentError.TransactionFailed, response);
        return new AllThreadDocuments(thread, postCounts, null, null);
    }
    
    public async Task<(IReadOnlyList<ThreadDocument>, IReadOnlyList<ThreadCountsDocument>)> GetUserThreadsDocumentsAsync(string userId, string? afterPostId, int limit, OperationContext context)
    {
        var key = ThreadDocument.KeyUserThreadsEnd(userId);

        const string sql = @"
            select * 
            from c 
            where c.pk = @pk 
              and c.isRootThread = true
              and c.sk < @id 
              and not is_defined(c.parentThreadUserId)
              and not is_defined(c.deleted)
            order by c.sk desc 
            offset 0 limit @limit";
        
        var query = new QueryDefinition(sql)
            .WithParameter("@pk", key.Pk)
            .WithParameter("@id", afterPostId == null ? key.Id : ThreadDocument.Key(userId, afterPostId).Id)
            .WithParameter("@limit", limit * 2);
        
        var posts = new List<ThreadDocument>();
        var postCounts = new List<ThreadCountsDocument>();
        await foreach (var document in ExecuteQueryReaderAsync(query, key.Pk, context))
        {
            if(document is ThreadDocument postDocument)
                posts.Add(postDocument);
            else if (document is ThreadCountsDocument postCountsDocument)
                postCounts.Add(postCountsDocument);
            else
                throw new InvalidOperationException("Unexpected document: " + document.GetType().Name);
        }

        return (posts, postCounts);
    }
    
    public async Task CreateCommentAsync(CommentDocument comment, OperationContext context)
    {
        var commentCounts = new CommentCountsDocument(comment.ThreadUserId, comment.ThreadId, comment.UserId, comment.CommentId, 0, 0, 0);
        var batch = Container.CreateTransactionalBatch(new PartitionKey(comment.Pk));
        batch.CreateItem(comment, _noResponse);
        batch.CreateItem(commentCounts, _noResponse);
        batch.PatchItem(ThreadCountsDocument.Key(comment.ThreadUserId, comment.ThreadId).Id, [PatchOperation.Increment( "/commentCount", 1)], _noPatchResponse);
        
        var response = await batch.ExecuteAsync(context.Cancellation);
        context.AddRequestCharge(response.RequestCharge);
        ThrowErrorIfTransactionFailed(ContentError.TransactionFailed, response);
    }
    
    public async Task<CommentDocument?> GetCommentAsync(string userId, string postId, string commentId, OperationContext context)
    {
        try
        {
            var key = CommentDocument.Key(userId, postId, commentId);
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
    
    public async Task<AllThreadDocuments> GetAllThreadDocumentsAsync(string userId, string postId, int lastCommentCount, OperationContext context)
    {
        var keyFrom = ThreadDocument.KeyThreadsItemsStart(userId, postId);
        var keyTo = ThreadDocument.KeyThreadItemsEnd(userId, postId);

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
        
        ThreadDocument? post = null;
        ThreadCountsDocument? postCounts = null;
        var comments = new List<CommentDocument>();
        var commentCounts = new List<CommentCountsDocument>();
        await foreach (var document in ExecuteQueryReaderAsync(query, keyFrom.Pk, context))
        {
            if(document is ThreadDocument postDocument)
                post = post == null ? postDocument : throw new InvalidOperationException("Expecting a single post.");
            else if(document is ThreadCountsDocument postCountsDocument)
                postCounts = postCounts == null ? postCountsDocument : throw new InvalidOperationException("Expecting a single post.");
            else if(document is CommentDocument commentDocument)
                comments.Add(commentDocument);
            else if(document is CommentCountsDocument commentCountsDocument)
                commentCounts.Add(commentCountsDocument);
            else
                throw new InvalidOperationException("Unexpected document: " + document.GetType().Name);
        }
        
        if(post == null || post is { Deleted: true })
            return new AllThreadDocuments(null, null, null, null);
        
        return new AllThreadDocuments(post, postCounts, comments, commentCounts);
    }
    
    public async Task<(List<CommentDocument>? comments, List<CommentCountsDocument>? commentCounts)> GetPreviousCommentsAsync(string userId, string postId, string commentId, int lastCommentCount, OperationContext context)
    {
        var key = CommentDocument.Key(userId, postId, commentId);
        var keyTo = ThreadDocument.KeyThreadsItemsStart(userId, postId);

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
    
    public async Task<ThreadDocument?> GetThreadDocumentAsync(string userId, string threadId, OperationContext context)
    {
        try
        {
            var key = ThreadDocument.Key(userId, threadId);
            var response = await Container.ReadItemAsync<ThreadDocument>(key.Id, new PartitionKey(key.Pk), _noResponseContent, context.Cancellation);
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
    
    public async Task RemoveThreadAsync(ThreadDocument document, OperationContext context)
    {
        var batch = Container.CreateTransactionalBatch(new PartitionKey(document.Pk));
        var deletePatch = new[] {PatchOperation.Set("/deleted", true), PatchOperation.Set("/ttl", _secondsInADay)};
        batch.PatchItem(document.Id, deletePatch, _noPatchResponse);
        batch.PatchItem(ThreadCountsDocument.Key(document.UserId, document.ThreadId).Id, deletePatch, _noPatchResponse);
        var response = await batch.ExecuteAsync(context.Cancellation);
        context.AddRequestCharge(response.RequestCharge);
        ThrowErrorIfTransactionFailed(ContentError.TransactionFailed, response);
    }
    
    public async Task RemoveCommentAsync(string parentThreadUserId, string parentThreadId, string commentId, OperationContext context)
    {
        var commentKey = CommentDocument.Key(parentThreadUserId, parentThreadId, commentId);
        var batch = Container.CreateTransactionalBatch(new PartitionKey(commentKey.Pk));
        var deletePatch = new[] {PatchOperation.Set("/deleted", true), PatchOperation.Set("/ttl", _secondsInADay)};
        batch.PatchItem(commentKey.Id, deletePatch, _noPatchResponse);
        batch.PatchItem(CommentCountsDocument.Key(parentThreadUserId, parentThreadId, commentId).Id, deletePatch, _noPatchResponse);
        batch.PatchItem(ThreadCountsDocument.Key(parentThreadUserId, parentThreadId).Id, [PatchOperation.Increment( "/commentCount", -1)], _noPatchResponse);
        var response = await batch.ExecuteAsync(context.Cancellation);
        context.AddRequestCharge(response.RequestCharge);
        ThrowErrorIfTransactionFailed(ContentError.TransactionFailed, response);
    }
    
    public async Task IncreaseViewsAsync(string userId, string postId, OperationContext context)
    {
        // TODO: Defer
        // Increase views
        var keyFrom = ThreadCountsDocument.Key(userId, postId);
        var response = await Container.PatchItemAsync<ThreadDocument>
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
}