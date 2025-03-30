using System.Net;
using Microsoft.Azure.Cosmos;
using SocialApp.WebApi.Data._Shared;
using SocialApp.WebApi.Data.User;
using SocialApp.WebApi.Features._Shared.Services;
using SocialApp.WebApi.Features.Content.Exceptions;
using SocialApp.WebApi.Features.Session.Models;

namespace SocialApp.WebApi.Features.Content.Containers;

public record struct AllPostDocuments(PostDocument? Post, PostCountsDocument? PostCounts, List<CommentDocument>? Comments, List<CommentCountsDocument>? CommentCounts);

public sealed class ContentContainer : CosmoContainer
{
    private static readonly TransactionalBatchPatchItemRequestOptions _noPatchResponse = new() {EnableContentResponseOnWrite = false};
    private static readonly ItemRequestOptions _noResponseContent = new(){ EnableContentResponseOnWrite = false};
    private static readonly TransactionalBatchItemRequestOptions _noResponse = new() { EnableContentResponseOnWrite = false };
    private static readonly PatchItemRequestOptions _patchItemNoResponse = new() { EnableContentResponseOnWrite = false};
    
    public ContentContainer(UserDatabase database)
        :base(database)
    {
    }
    
    public async Task<AllPostDocuments> CreatePostAsync(PostDocument post, OperationContext context)
    {
        var postCounts = new PostCountsDocument(post.UserId, post.PostId, 0, 0, 0, post.CommentUserId, post.CommentUserId);
        
        var batch = Container.CreateTransactionalBatch(new PartitionKey(post.Pk));
        batch.CreateItem(post, _noResponse);
        batch.CreateItem(postCounts, _noResponse);
        
        var response = await batch.ExecuteAsync(context.Cancellation);
        ThrowErrorIfTransactionFailed(ContentError.TransactionFailed, response);
        return new AllPostDocuments(post, postCounts, null, null);
    }
    
    public async Task<List<PostDocument>?> GetUserPostsAsync(string userId, string? afterPostId, int limit, OperationContext context)
    {
        var key = PostDocument.KeyUserPostsEnd(userId);

        const string sql = @"
            select * 
            from u 
            where u.pk = @pk 
              and u.sk < @id 
              and u.type in (@typePost, @typePostCounts) 
              and is_null(u.commentUserId)
            order by u.sk desc 
            offset 0 limit @limit";
        
        var query = new QueryDefinition(sql)
            .WithParameter("@pk", key.Pk)
            .WithParameter("@id", afterPostId == null ? key.Id : PostDocument.Key(userId, afterPostId).Id)
            .WithParameter("@typePost", nameof(PostDocument))
            .WithParameter("@typePostCounts", nameof(PostCountsDocument))
            .WithParameter("@limit", limit * 2);
        
        var posts = new List<PostDocument>();
        var postCounts = new List<PostCountsDocument>();
        await foreach (var document in MultiQueryAsync(query, context))
        {
            if(document is PostDocument postDocument)
                posts.Add(postDocument);
            else if (document is PostCountsDocument postCountsDocument)
                postCounts.Add(postCountsDocument);
            else
                throw new InvalidOperationException("Unexpected document: " + document.GetType().Name);
        }
        
        return posts;
    }
    
    public async Task CreateCommentAsync(CommentDocument comment, OperationContext context)
    {
        var commentCounts = new CommentCountsDocument(comment.UserId, comment.PostId, comment.ParentUserId, comment.ParentPostId, 0, 0, 0);
        var batch = Container.CreateTransactionalBatch(new PartitionKey(comment.Pk));
        batch.CreateItem(comment, _noResponse);
        batch.CreateItem(commentCounts, _noResponse);
        batch.PatchItem(PostCountsDocument.Key(comment.ParentUserId, comment.ParentPostId).Id, [PatchOperation.Increment( "/commentCount", 1)], _noPatchResponse);
        
        var response = await batch.ExecuteAsync(context.Cancellation);
        ThrowErrorIfTransactionFailed(ContentError.TransactionFailed, response);
    }
    
    public async Task UpdateCommentCountsAsync(string parentUserId, string parentPostId, OperationContext context)
    {
        // If parent is a comment in other post, it needs to update its comment count
        var parentKey = PostDocument.Key(parentUserId, parentPostId);
        var parent = await Container.ReadItemAsync<PostDocument>(parentKey.Id, new PartitionKey(parentKey.Pk), cancellationToken: context.Cancellation);
        if (!string.IsNullOrWhiteSpace(parent?.Resource?.CommentPostId) && !string.IsNullOrWhiteSpace(parent?.Resource?.CommentUserId))
        {
            var parentCommentCountsKey = CommentCountsDocument.Key(parent.Resource.CommentUserId, parent.Resource.CommentPostId, parentPostId);
            await Container.PatchItemAsync<CommentCountsDocument>
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
        var response = await Container.ReadItemAsync<CommentDocument>(key.Id, new PartitionKey(key.Pk), _noResponseContent, context.Cancellation);
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

        const string sql = @"
            select * from c 
            where c.pk = @pk 
              and c.sk >= @id 
              and c.sk < @id_end 
            order by c.sk desc 
            offset 0 limit @limit";
        var query = new QueryDefinition(sql)
            .WithParameter("@pk", keyFrom.Pk)
            .WithParameter("@id", keyFrom.Id)
            .WithParameter("@id_end", keyTo.Id)
            .WithParameter("@limit", 2 + lastCommentCount * 2 );

        var str = query.ToString();
        
        PostDocument? post = null;
        PostCountsDocument? postCounts = null;
        var comments = new List<CommentDocument>();
        var commentCounts = new List<CommentCountsDocument>();
        await foreach (var document in MultiQueryAsync(query, context))
        {
            if(document is PostDocument postDocument)
                post = post == null ? postDocument : throw new InvalidOperationException("Expecting a single post.");
            else if(document is PostCountsDocument postCountsDocument)
                postCounts = postCounts == null ? postCountsDocument : throw new InvalidOperationException("Expecting a single post.");
            else if(document is CommentDocument commentDocument)
                comments.Add(commentDocument);
            else if(document is CommentCountsDocument commentCountsDocument)
                commentCounts.Add(commentCountsDocument);
            else
                throw new InvalidOperationException("Unexpected document: " + document.GetType().Name);
        }
        
        if(post == null)
            return new AllPostDocuments(null, null, null, null);
        
        return new AllPostDocuments(post, postCounts, comments, commentCounts);
    }
    
    public async Task<(List<CommentDocument>? comments, List<CommentCountsDocument>? commentCounts)> GetPreviousCommentsAsync(string userId, string postId, string commentId, int lastCommentCount, OperationContext context)
    {
        var key = CommentDocument.Key(userId, postId, commentId);
        var keyTo = PostDocument.KeyPostItemsStart(userId, postId);

        const string sql = "select * from c where c.pk = @pk and c.sk < @id and c.sk > @id_end order by c.sk desc offset 0 limit @limit";
        
        var query = new QueryDefinition(sql)
            .WithParameter("@pk", key.Pk)
            .WithParameter("@id", key.Id)
            .WithParameter("@id_end", keyTo.Id)
            .WithParameter("@limit", lastCommentCount * 2);
        
        var comments = new List<CommentDocument>();
        var commentCounts = new List<CommentCountsDocument>();
        await foreach (var document in MultiQueryAsync(query, context))
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
    
    public async Task<(PostDocument?,PostCountsDocument?)> GetPostDocumentAsync(UserSession user, string postId, OperationContext context)
    {
        var keyFrom = PostDocument.Key(user.UserId, postId);
        var keyTo = PostCountsDocument.Key(user.UserId, postId);

        const string sql = "select * from c where c.pk = @pk and c.sk >= @id and c.sk <= @id_end";
        var query = new QueryDefinition(sql)
            .WithParameter("@pk", keyFrom.Pk)
            .WithParameter("@id", keyFrom.Id)
            .WithParameter("@id_end", keyTo.Id);
        
        PostDocument? post = null;
        PostCountsDocument? postCounts = null;
        await foreach (var document in MultiQueryAsync(query, context))
        {
            if(document is PostDocument postDocument)
                post = post == null ? postDocument : throw new InvalidOperationException("Expecting a single post.");
            else if(document is PostCountsDocument postCountsDocument)
                postCounts = postCounts == null ? postCountsDocument : throw new InvalidOperationException("Expecting a single post counts.");
            else
                throw new InvalidOperationException("Unexpected document: " + document.GetType().Name);
        }
        return (post,postCounts);
    }
    
    public async Task ReplaceDocumentAsync<T>(T document, OperationContext context)
        where T : Document
    {
        await Container.ReplaceItemAsync
        (
            document,
            document.Id, new PartitionKey(document.Pk),
            new ItemRequestOptions { IfMatchEtag = document.ETag, EnableContentResponseOnWrite = false },
            context.Cancellation
        );
    }
    
    public async Task RemovePostAsync(PostDocument document, OperationContext context)
    {
        var batch = Container.CreateTransactionalBatch(new PartitionKey(document.Pk));
        batch.DeleteItem(document.Id, _noResponse);
        batch.DeleteItem(PostCountsDocument.Key(document.UserId, document.PostId).Id, _noResponse);
        var response = await batch.ExecuteAsync(context.Cancellation);
        ThrowErrorIfTransactionFailed(ContentError.TransactionFailed, response);
    }
    
    public async Task RemoveCommentAsync(CommentDocument document, OperationContext context)
    {
        var batch = Container.CreateTransactionalBatch(new PartitionKey(document.Pk));
        batch.DeleteItem(document.Id, _noResponse);
        batch.DeleteItem(CommentCountsDocument.Key(document.ParentUserId, document.ParentPostId, document.PostId).Id, _noResponse);
        batch.PatchItem(PostCountsDocument.Key(document.ParentUserId, document.ParentPostId).Id, [PatchOperation.Increment( "/commentCount", -1)], _noPatchResponse);
        var response = await batch.ExecuteAsync(context.Cancellation);
        ThrowErrorIfTransactionFailed(ContentError.TransactionFailed, response);
    }
    
    public async Task IncreaseViewsAsync(string userId, string postId, OperationContext context)
    {
        // TODO: Defer
        // Increase views
        var keyFrom = PostCountsDocument.Key(userId, postId);
        await Container.PatchItemAsync<PostDocument>
        (
            keyFrom.Id,
            new PartitionKey(keyFrom.Pk),
            [PatchOperation.Increment("/viewCount", 1)],
            _patchItemNoResponse, 
            context.Cancellation
        );
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