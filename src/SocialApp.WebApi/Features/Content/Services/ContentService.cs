using System.Net;
using System.Text.Json;
using Microsoft.Azure.Cosmos;
using SocialApp.WebApi.Features.Content.Databases;
using SocialApp.WebApi.Features.Content.Documents;
using SocialApp.WebApi.Features.Content.Exceptions;
using SocialApp.WebApi.Features.Content.Models;
using SocialApp.WebApi.Features.Services;
using SocialApp.WebApi.Features.Session.Models;

namespace SocialApp.WebApi.Features.Content.Services;

public sealed class ContentService
{
    private static readonly TransactionalBatchPatchItemRequestOptions _noPatchResponse = new() {EnableContentResponseOnWrite = false};
    private static readonly ItemRequestOptions _noResponseContent = new(){ EnableContentResponseOnWrite = false};
    private static readonly TransactionalBatchItemRequestOptions _noResponse = new() { EnableContentResponseOnWrite = false };
    private static readonly PatchItemRequestOptions _patchItemNoResponse = new PatchItemRequestOptions() { EnableContentResponseOnWrite = false};
    
    private readonly ContentDatabase _contentDb;
    public ContentService(ContentDatabase contentDb)
        => _contentDb = contentDb;

    public async ValueTask<string> CreatePostAsync(UserSession user, string content, OperationContext context)
    {
        var post = new PostDocument(user.UserId, Ulid.NewUlid(context.UtcNow).ToString(), content, context.UtcNow.UtcDateTime, null, null);
        var postCounts = new PostCountsDocument(user.UserId, post.PostId, 0, 0, 0, context.UtcNow.UtcDateTime, null, null);
        var contents = _contentDb.GetContainer();
        var batch = contents.CreateTransactionalBatch(new PartitionKey(post.Pk));
        batch.CreateItem(post);
        batch.CreateItem(postCounts);
        var response = await batch.ExecuteAsync(context.Cancellation);
        ThrowErrorIfTransactionFailed(ContentError.CreatePostFailure, response);
        return post.PostId;
    }
    
    public async ValueTask<string> CommentAsync(UserSession user, string parentUserId, string parentPostId, string content, OperationContext context)
    {
        // Creates own Post
        var post = new PostDocument(user.UserId, Ulid.NewUlid(context.UtcNow).ToString(), content, context.UtcNow.UtcDateTime, parentUserId, parentPostId);
        var postCounts = new PostCountsDocument(user.UserId, post.PostId, 0, 0, 0, context.UtcNow.UtcDateTime, parentUserId, parentPostId);
        var contents = _contentDb.GetContainer();
        var batch = contents.CreateTransactionalBatch(new PartitionKey(post.Pk));
        batch.CreateItem(post, _noResponse);
        batch.CreateItem(postCounts, _noResponse);
        
        var response = await batch.ExecuteAsync(context.Cancellation);
        ThrowErrorIfTransactionFailed(ContentError.CreateCommentPostFailure, response);
        
        // Creates Comment in parent post
        var comment = new CommentDocument(user.UserId, post.PostId, parentUserId, parentPostId, content, context.UtcNow.UtcDateTime);
        var commentCounts = new CommentCountsDocument(user.UserId, post.PostId, parentUserId, parentPostId, 0, 0, 0, context.UtcNow.UtcDateTime);
        batch = contents.CreateTransactionalBatch(new PartitionKey(comment.Pk));
        batch.CreateItem(comment, _noResponse);
        batch.CreateItem(commentCounts, _noResponse);
        batch.PatchItem(PostCountsDocument.Key(parentUserId, parentPostId).Id, [PatchOperation.Increment( $"/{nameof(CommentCountsDocument.CommentCount)}", 1)], _noPatchResponse);
        
        response = await batch.ExecuteAsync(context.Cancellation);
        ThrowErrorIfTransactionFailed(ContentError.CreateCommentFailure, response);
        
        // If parent is a comment in other post, it needs to update its comment count
        var parentKey = PostDocument.Key(parentUserId, parentPostId);
        var parent = await contents.ReadItemAsync<PostDocument>(parentKey.Id, new PartitionKey(parentKey.Pk), cancellationToken: context.Cancellation);
        if (!string.IsNullOrWhiteSpace(parent?.Resource?.CommentPostId) && !string.IsNullOrWhiteSpace(parent?.Resource?.CommentUserId))
        {
            var parentCommentCountsKey = CommentCountsDocument.Key(parent.Resource.CommentUserId, parent.Resource.CommentPostId, parentPostId);
            await contents.PatchItemAsync<CommentCountsDocument>
            (
                parentCommentCountsKey.Id, 
                new PartitionKey(parentCommentCountsKey.Pk), 
                [PatchOperation.Increment($"/{nameof(CommentCountsDocument.CommentCount)}",1)],
                _patchItemNoResponse,
                cancellationToken: context.Cancellation
            );
        }
        
        return post.PostId;
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

    private object? Discriminate(JsonElement item)
    {
        var type = item.GetProperty("type").GetString();
        return type switch
        {
            nameof(PostDocument) => _contentDb.Deserialize<PostDocument>(item),
            nameof(CommentDocument) => _contentDb.Deserialize<CommentDocument>(item),
            nameof(PostCountsDocument) => _contentDb.Deserialize<PostCountsDocument>(item),
            nameof(CommentCountsDocument) => _contentDb.Deserialize<CommentCountsDocument>(item),
            _ => null
        };
    }
    
    public async ValueTask<Post?> GetPostAsync(string userId, string postId, int lastCommentCount, OperationContext context)
    {
        var keyFrom = PostDocument.KeyPostItemsStart(userId, postId);
        var keyTo = PostDocument.KeyPostItemsEnd(userId, postId);

        const string sql = "select * from u where u.pk = @pk and u.id >= @id and u.id < @id_end order by u.id desc offset 0 limit @limit";
        var query = new QueryDefinition(sql)
            .WithParameter("@pk", keyFrom.Pk)
            .WithParameter("@id", keyFrom.Id)
            .WithParameter("@id_end", keyTo.Id)
            .WithParameter("@limit", 2 + lastCommentCount * 2 );

        var contents = _contentDb.GetContainer();
        var (post, postCounts, comments, commentCounts) = await ResolveContentQueryAsync(contents, query, context);

        if (post == null)
            return null;
        
        var model = Post.From(post);
        model.CommentCount = postCounts.CommentCount;
        model.ViewCount = postCounts.ViewCount +1;
        model.LikeCount = postCounts.LikeCount;

        if (comments != null)
        {
            for (var i = 0; i < comments.Count; i++)
            {
                var comment = Comment.From(comments[i]);
                var commentCount = commentCounts[i];
                Comment.Apply(comment, commentCount);
                model.LastComments.Add(comment);
            }
            model.LastComments.Reverse();
        }
        await IncreaseViewsAsync(post, contents, context);
        return model;
    }
    
    public async ValueTask<IReadOnlyList<Comment>> GetPreviousCommentsAsync(string userId, string postId, string commentId, int lastCommentCount, OperationContext context)
    {
        var key = CommentDocument.Key(userId, postId, commentId);
        var keyTo = PostDocument.KeyPostItemsStart(userId, postId);

        const string sql = "select * from u where u.pk = @pk and u.id < @id and u.id > @id_end order by u.id desc offset 0 limit @limit";
        
        var query = new QueryDefinition(sql)
            .WithParameter("@pk", key.Pk)
            .WithParameter("@id", key.Id)
            .WithParameter("@id_end", keyTo.Id)
            .WithParameter("@limit", lastCommentCount * 2);

        var contents = _contentDb.GetContainer();
        var (_, _, comments, commentCounts) = await ResolveContentQueryAsync(contents, query, context);
        if (comments == null)
            return Array.Empty<Comment>();

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
    
    public async ValueTask<IReadOnlyList<Post>> GetUserPostsAsync(string userId, string? afterPostId, int limit, OperationContext context)
    {
        var key = PostDocument.KeyPostsEnd(userId);

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

        var contents = _contentDb.GetContainer();
        var (posts, postCounts) = await ResolvePostContentQueryAsync(contents, query, context);
        if (posts == null)
            return Array.Empty<Post>();
        
        var postsModels = new List<Post>(posts.Count);
        for (var i = 0; i < posts.Count; i++)
        {
            var post = Post.From(posts[i]);
            postsModels.Add(post);
        }
        return postsModels;
    }

    private static async Task IncreaseViewsAsync(PostDocument post, Container contents, OperationContext context)
    {
        // TODO: Defer
        // Increase views
        var keyFrom = PostCountsDocument.Key(post.UserId, post.PostId);
        await contents.PatchItemAsync<PostDocument>
        (
            keyFrom.Id,
            new PartitionKey(keyFrom.Pk),
            [PatchOperation.Increment($"/{nameof(PostCountsDocument.ViewCount)}", 1)],
            _patchItemNoResponse, 
            context.Cancellation
        );
    }
        private async Task<(PostDocument?, PostCountsDocument?, List<CommentDocument>?, List<CommentCountsDocument>?)> ResolveContentQueryAsync(Container contents, QueryDefinition postQuery, OperationContext context)
    {
        PostDocument? post = null;
        PostCountsDocument? postCounts = null;
        List<CommentDocument>? comments = null;
        List<CommentCountsDocument>? commentCounts = null;
        
        using var itemIterator = contents.GetItemQueryIterator<JsonElement>(postQuery);

        while (itemIterator.HasMoreResults)
        {
            var items = await itemIterator.ReadNextAsync(context.Cancellation);
            foreach (var item in items)
            {
                var document = Discriminate(item);

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
    
    private async Task<(List<PostDocument>?, List<PostCountsDocument>?)> ResolvePostContentQueryAsync(Container contents, QueryDefinition postQuery, OperationContext context)
    {
        List<PostDocument>? posts = null;
        List<PostCountsDocument>? postCounts = null;
        
        using var itemIterator = contents.GetItemQueryIterator<JsonElement>(postQuery);

        while (itemIterator.HasMoreResults)
        {
            var items = await itemIterator.ReadNextAsync(context.Cancellation);
            foreach (var item in items)
            {
                var document = Discriminate(item);

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

}