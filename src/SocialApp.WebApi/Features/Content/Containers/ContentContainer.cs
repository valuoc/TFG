using System.Net;
using System.Text.Json;
using Microsoft.Azure.Cosmos;
using SocialApp.WebApi.Data._Shared;
using SocialApp.WebApi.Data.User;
using SocialApp.WebApi.Features._Shared.Services;
using SocialApp.WebApi.Features.Content.Exceptions;

namespace SocialApp.WebApi.Features.Content.Containers;

public record struct AllPostDocuments(PostDocument? Post, PostCountsDocument? PostCounts, List<CommentDocument>? Comments, List<CommentCountsDocument>? CommentCounts);

public sealed class ContentContainer
{
    private static readonly TransactionalBatchPatchItemRequestOptions _noPatchResponse = new() {EnableContentResponseOnWrite = false};
    private static readonly ItemRequestOptions _noResponseContent = new(){ EnableContentResponseOnWrite = false};
    private static readonly TransactionalBatchItemRequestOptions _noResponse = new() { EnableContentResponseOnWrite = false };
    private static readonly PatchItemRequestOptions _patchItemNoResponse = new() { EnableContentResponseOnWrite = false};
    
    private readonly Container _container;
    private readonly UserDatabase _database;

    public ContentContainer(UserDatabase database)
    {
        _container = database.GetContainer();
        _database = database;
    }
    
    public async Task<PendingCommentsDocument> RegisterPendingCommentActionAsync(string userId, string parentUserId, string parentPostId, string postId, PendingCommentOperation operation, OperationContext context)
    {
        var pendingKey = PendingCommentsDocument.Key(userId);
        var pendingComment = new PendingComment(userId, postId, parentUserId, parentPostId, operation);
        var pendingCommentResponse = await _container.PatchItemAsync<PendingCommentsDocument>
        (
            pendingKey.Id, new PartitionKey(pendingKey.Pk),
            [PatchOperation.Add("/items/-", pendingComment)], // patch is case sensitive
            cancellationToken: context.Cancellation
        );
        var pending = pendingCommentResponse.Resource;
        pending.ETag = pendingCommentResponse.ETag;
        return pending;
    }
    
    public async Task ClearPendingCommentActionAsync(PendingCommentsDocument pending, string postId, OperationContext context)
    {
        var index = pending.Items.Select((c, i) => (c, i)).First(x => x.c.PostId == postId).i;
        await _container.PatchItemAsync<PendingCommentsDocument>
        (
            pending.Id, new PartitionKey(pending.Pk),
            [PatchOperation.Remove($"/items/{index}")], // patch is case sensitive
            new PatchItemRequestOptions
            {
                EnableContentResponseOnWrite = false,
                IfMatchEtag = pending.ETag
            },
            cancellationToken: context.Cancellation
        );
    }
    
    public async Task<PendingCommentsDocument> GetPendingCommentAsync(string userId, OperationContext context)
    {
        var pendingKey = PendingCommentsDocument.Key(userId);
        var pendingCommentResponse = await _container.ReadItemAsync<PendingCommentsDocument>
        (
            pendingKey.Id, new PartitionKey(pendingKey.Pk),
            cancellationToken: context.Cancellation
        );
        var pending = pendingCommentResponse.Resource;
        pending.ETag = pendingCommentResponse.ETag;
        return pending;
    }
    
    //
    
    public async Task<AllPostDocuments> CreatePostAsync(string userId, string? parentUserId, string? parentPostId, string postId, string content, OperationContext context)
    {
        var post = new PostDocument(userId, postId, content, context.UtcNow.UtcDateTime, 0, parentUserId, parentPostId);
        var postCounts = new PostCountsDocument(userId, postId, 0, 0, 0, parentUserId, parentPostId);
        var batch = _container.CreateTransactionalBatch(new PartitionKey(post.Pk));
        batch.CreateItem(post, _noResponse);
        batch.CreateItem(postCounts, _noResponse);
        
        var response = await batch.ExecuteAsync(context.Cancellation);
        ThrowErrorIfTransactionFailed(ContentError.CreateCommentPostFailure, response);
        return new AllPostDocuments(post, postCounts, null, null);
    }
    
    public async Task<List<PostDocument>?> GetUserPostsAsync(string userId, string? afterPostId, int limit, OperationContext context)
    {
        var key = PostDocument.KeyUserPostsEnd(userId);

        const string sql = @"
            select * 
            from u 
            where u.pk = @pk 
              and u.id < @id 
              and u.type in (@typePost, @typePostCounts) 
              and is_null(u.commentUserId)
            order by u.id desc 
            offset 0 limit @limit";
        
        var query = new QueryDefinition(sql)
            .WithParameter("@pk", key.Pk)
            .WithParameter("@id", afterPostId == null ? key.Id : PostDocument.Key(userId, afterPostId).Id)
            .WithParameter("@typePost", nameof(PostDocument))
            .WithParameter("@typePostCounts", nameof(PostCountsDocument))
            .WithParameter("@limit", limit * 2);
        
        var (posts, postCounts) = await ResolvePostQueryAsync(query, context);
        return posts;
    }
    
    //
    
    public async Task CreateCommentAsync(string userId, string parentUserId, string parentPostId, string content, string postId, OperationContext context)
    {
        var comment = new CommentDocument(userId, postId, parentUserId, parentPostId, content, context.UtcNow.UtcDateTime, 0);
        var commentCounts = new CommentCountsDocument(userId, postId, parentUserId, parentPostId, 0, 0, 0);
        var batch = _container.CreateTransactionalBatch(new PartitionKey(comment.Pk));
        batch.CreateItem(comment, _noResponse);
        batch.CreateItem(commentCounts, _noResponse);
        batch.PatchItem(PostCountsDocument.Key(parentUserId, parentPostId).Id, [PatchOperation.Increment( "/commentCount", 1)], _noPatchResponse);
        
        var response = await batch.ExecuteAsync(context.Cancellation);
        ThrowErrorIfTransactionFailed(ContentError.CreateCommentFailure, response);
    }
    
    public async Task UpdateCommentCountsAsync(string parentUserId, string parentPostId, OperationContext context)
    {
        // If parent is a comment in other post, it needs to update its comment count
        var parentKey = PostDocument.Key(parentUserId, parentPostId);
        var parent = await _container.ReadItemAsync<PostDocument>(parentKey.Id, new PartitionKey(parentKey.Pk), cancellationToken: context.Cancellation);
        if (!string.IsNullOrWhiteSpace(parent?.Resource?.CommentPostId) && !string.IsNullOrWhiteSpace(parent?.Resource?.CommentUserId))
        {
            var parentCommentCountsKey = CommentCountsDocument.Key(parent.Resource.CommentUserId, parent.Resource.CommentPostId, parentPostId);
            await _container.PatchItemAsync<CommentCountsDocument>
            (
                parentCommentCountsKey.Id, 
                new PartitionKey(parentCommentCountsKey.Pk), 
                [PatchOperation.Increment("/commentCount",1)],
                _patchItemNoResponse,
                cancellationToken: context.Cancellation
            );
        }
    }
    
    public async Task<CommentDocument?> GetCommentAsync(string userId, string postId, string commentId, OperationContext context)
    {
        var key = CommentDocument.Key(userId, postId, commentId);
        var response = await _container.ReadItemAsync<CommentDocument>(key.Id, new PartitionKey(key.Pk), _noResponseContent, context.Cancellation);
        if(response.Resource != null)
        {
            var comment = response.Resource;
            comment.ETag = response.ETag;
            return comment;
        }
        return null;
    }
    
    //
    
    public async Task<AllPostDocuments> GetAllPostDocumentsAsync(string userId, string postId, int lastCommentCount, OperationContext context)
    {
        var keyFrom = PostDocument.KeyPostItemsStart(userId, postId);
        var keyTo = PostDocument.KeyPostItemsEnd(userId, postId);

        const string sql = "select * from u where u.pk = @pk and u.id >= @id and u.id < @id_end order by u.id desc offset 0 limit @limit";
        var query = new QueryDefinition(sql)
            .WithParameter("@pk", keyFrom.Pk)
            .WithParameter("@id", keyFrom.Id)
            .WithParameter("@id_end", keyTo.Id)
            .WithParameter("@limit", 2 + lastCommentCount * 2 );
        
        var (post, postCounts, comments, commentCounts) = await ResolvePostWithCommentsQueryAsync(query, context);
        if (post == null)
            return await TryRecoverPostDocumentsAsync(userId, postId, context);
        
        return new AllPostDocuments(post, postCounts, comments, commentCounts);
    }
    
    public async Task<(List<CommentDocument>? comments, List<CommentCountsDocument>? commentCounts)> GetPreviousCommentsAsync(string userId, string postId, string commentId, int lastCommentCount, OperationContext context)
    {
        var key = CommentDocument.Key(userId, postId, commentId);
        var keyTo = PostDocument.KeyPostItemsStart(userId, postId);

        const string sql = "select * from u where u.pk = @pk and u.id < @id and u.id > @id_end order by u.id desc offset 0 limit @limit";
        
        var query = new QueryDefinition(sql)
            .WithParameter("@pk", key.Pk)
            .WithParameter("@id", key.Id)
            .WithParameter("@id_end", keyTo.Id)
            .WithParameter("@limit", lastCommentCount * 2);
        
        var (_, _, comments, commentCounts) = await ResolvePostWithCommentsQueryAsync(query, context);
        return (comments, commentCounts);
    }
    
    public async Task<PostDocument?> GetPostDocumentAsync(string userId, string postId, OperationContext context)
    {
        var keyFrom = PostDocument.Key(userId, postId);

        const string sql = "select * from u where u.pk = @pk and u.id = @id";
        var query = new QueryDefinition(sql)
            .WithParameter("@pk", keyFrom.Pk)
            .WithParameter("@id", keyFrom.Id);
        
        var (post, _, _, _) = await ResolvePostWithCommentsQueryAsync(query, context);
        if (post == null)
        {
            var recovered = await TryRecoverPostDocumentsAsync(userId, postId, context);
            return recovered.Post;
        }

        return post;
    }
    
    //
    
    public async Task ReplaceDocumentAsync<T>(T document, OperationContext context)
        where T:Document
    {
        await _container.ReplaceItemAsync
        (
            document,
            document.Id, new PartitionKey(document.Pk),
            new ItemRequestOptions { IfMatchEtag = document.ETag, EnableContentResponseOnWrite = false },
            context.Cancellation
        );
    }
    
    public async Task IncreaseViewsAsync(string userId, string postId, OperationContext context)
    {
        // TODO: Defer
        // Increase views
        var keyFrom = PostCountsDocument.Key(userId, postId);
        await _container.PatchItemAsync<PostDocument>
        (
            keyFrom.Id,
            new PartitionKey(keyFrom.Pk),
            [PatchOperation.Increment("/viewCount", 1)],
            _patchItemNoResponse, 
            context.Cancellation
        );
    }
    
    private Document? DeserializeDocument(JsonElement item)
    {
        var type = item.GetProperty("type").GetString();
        Document? doc = type switch
        {
            nameof(PostDocument) => _database.Deserialize<PostDocument>(item),
            nameof(CommentDocument) => _database.Deserialize<CommentDocument>(item),
            nameof(PostCountsDocument) => _database.Deserialize<PostCountsDocument>(item),
            nameof(CommentCountsDocument) => _database.Deserialize<CommentCountsDocument>(item),
            _ => null
        };
        if (doc != null)
            doc.ETag = item.GetProperty("_etag").GetString();

        return doc;
    }
    
    private async Task<(PostDocument?, PostCountsDocument?, List<CommentDocument>?, List<CommentCountsDocument>?)> ResolvePostWithCommentsQueryAsync(QueryDefinition postQuery, OperationContext context)
    {
        PostDocument? post = null;
        PostCountsDocument? postCounts = null;
        List<CommentDocument>? comments = null;
        List<CommentCountsDocument>? commentCounts = null;
        
        using var itemIterator = _container.GetItemQueryIterator<JsonElement>(postQuery);

        while (itemIterator.HasMoreResults)
        {
            var items = await itemIterator.ReadNextAsync(context.Cancellation);
            foreach (var item in items)
            {
                var document = DeserializeDocument(item);

                switch (document)
                {
                    case PostDocument postDocument:
                        post = postDocument;
                        break;
                    
                    case PostCountsDocument counts:
                        postCounts = counts;
                        break;
                    
                    case CommentDocument commentDocument:
                        comments ??= new List<CommentDocument>();
                        comments.Add(commentDocument);
                        break;
                    
                    case CommentCountsDocument counts:
                        commentCounts ??= new List<CommentCountsDocument>();
                        commentCounts.Add(counts);
                        break;
                }
            }
        }
        return (post, postCounts, comments, commentCounts);
    }
    
    private async Task<(List<PostDocument>?, List<PostCountsDocument>?)> ResolvePostQueryAsync(QueryDefinition postQuery, OperationContext context)
    {
        List<PostDocument>? posts = null;
        List<PostCountsDocument>? postCounts = null;
        
        using var itemIterator = _container.GetItemQueryIterator<JsonElement>(postQuery);

        while (itemIterator.HasMoreResults)
        {
            var items = await itemIterator.ReadNextAsync(context.Cancellation);
            foreach (var item in items)
            {
                var document = DeserializeDocument(item);

                switch (document)
                {
                    case PostDocument postDocument:
                        posts ??= new List<PostDocument>();
                        posts.Add(postDocument);
                        break;
                    
                    case PostCountsDocument counts:
                        postCounts ??= new List<PostCountsDocument>();
                        postCounts.Add(counts);
                        break;
                }
            }
        }
        return (posts, postCounts);
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
    
    private async Task<AllPostDocuments> TryRecoverPostDocumentsAsync(string userId, string postId, OperationContext context)
    {
        var pendingComments = await GetPendingCommentAsync(userId, context);
        var pending = pendingComments?.Items?.SingleOrDefault(x => x.PostId == postId);
        if (pending != null)
        {
            var comment = await GetCommentAsync(pending.ParentUserId, pending.ParentPostId, pending.PostId, context);
            // If pending is retried, parent counts could be updated more than once. 
            await UpdateCommentCountsAsync(pending.ParentUserId, pending.ParentPostId, context);
            var postDocuments = await CreatePostAsync(userId, pending.ParentUserId, pending.ParentPostId, postId, comment.Content, context);
            await ClearPendingCommentActionAsync(pendingComments!, postId, context);
                
            // Because it was missing, it cannot have comments, views or likes
            return postDocuments;
        }

        return new AllPostDocuments(null, null, null, null);
    }
    
}