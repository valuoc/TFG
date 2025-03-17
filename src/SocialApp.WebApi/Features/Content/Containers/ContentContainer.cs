using System.Net;
using System.Text.Json;
using Microsoft.Azure.Cosmos;
using SocialApp.WebApi.Data._Shared;
using SocialApp.WebApi.Data.User;
using SocialApp.WebApi.Features._Shared.Services;
using SocialApp.WebApi.Features.Content.Exceptions;
using SocialApp.WebApi.Features.Session.Models;

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
    
    public async Task<AllPostDocuments> CreatePostAsync(PostDocument post, PostCountsDocument postCounts, OperationContext context)
    {
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
    
    public async Task CreateCommentAsync(CommentDocument comment, CommentCountsDocument commentCounts, OperationContext context)
    {
        var batch = _container.CreateTransactionalBatch(new PartitionKey(comment.Pk));
        batch.CreateItem(comment, _noResponse);
        batch.CreateItem(commentCounts, _noResponse);
        batch.PatchItem(PostCountsDocument.Key(comment.ParentUserId, comment.ParentPostId).Id, [PatchOperation.Increment( "/commentCount", 1)], _noPatchResponse);
        
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
    
    public async Task<AllPostDocuments> GetAllPostDocumentsAsync(UserSession user, string postId, int lastCommentCount, OperationContext context)
    {
        var keyFrom = PostDocument.KeyPostItemsStart(user.UserId, postId);
        var keyTo = PostDocument.KeyPostItemsEnd(user.UserId, postId);

        const string sql = "select * from u where u.pk = @pk and u.id >= @id and u.id < @id_end order by u.id desc offset 0 limit @limit";
        var query = new QueryDefinition(sql)
            .WithParameter("@pk", keyFrom.Pk)
            .WithParameter("@id", keyFrom.Id)
            .WithParameter("@id_end", keyTo.Id)
            .WithParameter("@limit", 2 + lastCommentCount * 2 );
        
        var (post, postCounts, comments, commentCounts) = await ResolvePostWithCommentsQueryAsync(query, context);
        if (post == null)
            return new AllPostDocuments(null, null, null, null);
        
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
    
    public async Task<PostDocument?> GetPostDocumentAsync(UserSession user, string postId, OperationContext context)
    {
        var keyFrom = PostDocument.Key(user.UserId, postId);

        const string sql = "select * from u where u.pk = @pk and u.id = @id";
        var query = new QueryDefinition(sql)
            .WithParameter("@pk", keyFrom.Pk)
            .WithParameter("@id", keyFrom.Id);
        
        var (post, _, _, _) = await ResolvePostWithCommentsQueryAsync(query, context);
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
}