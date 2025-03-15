using System.Net;
using System.Text.Json;
using Microsoft.Azure.Cosmos;
using SocialApp.WebApi.Features.Content.Databases;
using SocialApp.WebApi.Features.Content.Documents;
using SocialApp.WebApi.Features.Content.Exceptions;
using SocialApp.WebApi.Features.Content.Models;
using SocialApp.WebApi.Features.Documents;
using SocialApp.WebApi.Features.Services;
using SocialApp.WebApi.Features.Session.Models;

namespace SocialApp.WebApi.Features.Content.Services;

public sealed class ContentService
{
    private static readonly TransactionalBatchPatchItemRequestOptions _noPatchResponse = new() {EnableContentResponseOnWrite = false};
    private static readonly ItemRequestOptions _noResponseContent = new(){ EnableContentResponseOnWrite = false};
    private static readonly TransactionalBatchItemRequestOptions _noResponse = new() { EnableContentResponseOnWrite = false };
    
    private readonly ContentDatabase _contentDb;

    public ContentService(ContentDatabase contentDb)
    {
        _contentDb = contentDb;
    }

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
        var post = new PostDocument(user.UserId, Ulid.NewUlid(context.UtcNow).ToString(), content, context.UtcNow.UtcDateTime, parentUserId, parentPostId);
        var postCounts = new PostCountsDocument(user.UserId, post.PostId, 0, 0, 0, context.UtcNow.UtcDateTime, parentUserId, parentPostId);
        var contents = _contentDb.GetContainer();
        var batch = contents.CreateTransactionalBatch(new PartitionKey(post.Pk));
        batch.CreateItem(post, _noResponse);
        batch.CreateItem(postCounts, _noResponse);
        
        var response = await batch.ExecuteAsync(context.Cancellation);
        ThrowErrorIfTransactionFailed(ContentError.CreateCommentPostFailure, response);
        
        var comment = new CommentDocument(user.UserId, post.PostId, parentUserId, parentPostId, content, context.UtcNow.UtcDateTime);
        var commentCounts = new CommentCountsDocument(user.UserId, post.PostId, parentUserId, parentPostId, 0, 0, 0, context.UtcNow.UtcDateTime);
        batch = contents.CreateTransactionalBatch(new PartitionKey(comment.Pk));
        batch.CreateItem(comment, _noResponse);
        batch.CreateItem(commentCounts, _noResponse);
        batch.PatchItem(PostCountsDocument.Key(parentUserId, parentPostId).Id, [PatchOperation.Increment( $"/{nameof(CommentCountsDocument.CommentCount)}", 1)]);
        
        response = await batch.ExecuteAsync(context.Cancellation);
        ThrowErrorIfTransactionFailed(ContentError.CreateCommentFailure, response);
        
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

    public async ValueTask<Post?> GetPostAsync(string userId, string postId, int lastCommentCount, OperationContext context)
    {
        var keyFrom = PostDocument.KeyItemsStart(userId, postId);
        var keyTo = PostDocument.KeyItemsEnd(userId, postId);
        
        var query = new QueryDefinition("select * from u where u.pk = @pk and u.id >= @id and u.id < @id_end order by u.id desc offset 0 limit @limit")
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
        var keyTo = PostDocument.KeyItemsStart(userId, postId);

        var query = new QueryDefinition("select * from u where u.pk = @pk and u.id < @id and u.id > @id_end order by u.id desc offset 0 limit @limit")
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

    private static async Task IncreaseViewsAsync(PostDocument post, Container contents, OperationContext context)
    {
        DocumentKey keyFrom;
        // TODO: Defer
        // Increase views
        keyFrom = PostCountsDocument.Key(post.UserId, post.PostId);
        await contents.PatchItemAsync<PostDocument>
        (
            keyFrom.Id,
            new PartitionKey(keyFrom.Pk),
            [PatchOperation.Increment($"/{nameof(PostCountsDocument.ViewCount)}", 1)],
            new PatchItemRequestOptions() { EnableContentResponseOnWrite = false }, 
            context.Cancellation
        );
    }

    public ValueTask<IReadOnlyList<string>> GetAllPostsAsync(string userId, OperationContext context)
    {
        return ValueTask.FromResult((IReadOnlyList<string>)new List<string>().AsReadOnly());
    }
}