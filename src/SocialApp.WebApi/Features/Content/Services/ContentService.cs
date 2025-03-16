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
        var postId = Ulid.NewUlid(context.UtcNow).ToString();
        var contents = _contentDb.GetContainer();
        await CreatePostAsync(contents, user, null, null, postId, content, context);
        return postId;
    }
    
    public async ValueTask<string> CommentAsync(UserSession user, string parentUserId, string parentPostId, string content, OperationContext context)
    {
        try
        {
            var postId = Ulid.NewUlid(context.UtcNow).ToString();
            var contents = _contentDb.GetContainer();

            var pending = await RegisterPendingCommentAsync(contents, user, parentUserId, parentPostId, postId, context);
            context.Signal("create-comment");
            await CreateCommentInParentPostAsync(contents, user, parentUserId, parentPostId, content, postId, context);
            context.Signal("create-comment-post");
            await CreatePostAsync(contents, user, parentUserId, parentPostId, postId, content, context);
            context.Signal("update-parent-post");
            await UpdateParentPostCommentCountsAsync(contents, parentUserId, parentPostId, context);
            context.Signal("clear-pending-comment");
            await ClearPendingCommentAsync(contents, pending, postId, context);
            return postId;
        }
        catch (ContentException)
        {
            throw;
        }
        catch (Exception e)
        {
            throw new ContentException(ContentError.UnexpectedError, e);
        }
    }
    
    public async ValueTask<Post?> GetPostAsync(UserSession user, string postId, int lastCommentCount, OperationContext context)
    {
        var contents = _contentDb.GetContainer();
        
        var model = await TryGetPostFromDbAsync(user.UserId, postId, lastCommentCount, context, contents);
        if(model == null)
        {
            var pendingComments = await GetPendingCommentAsync(contents, user.UserId, context);
            var pending = pendingComments?.Items?.SingleOrDefault(x => x.PostId == postId);
            if (pending != null)
            {
                var comment = await GetCommentAsync(pending.ParentUserId, pending.ParentPostId, pending.PostId, context);
                // If pending is retried, parent counts could be updated more than once. 
                await UpdateParentPostCommentCountsAsync(contents, pending.ParentUserId, pending.ParentPostId, context);
                var postDocument = await CreatePostAsync(contents, user, pending.ParentUserId, pending.ParentPostId, postId, comment.Content, context);
                await ClearPendingCommentAsync(contents, pendingComments!, postId, context);
                
                // Becasue it was missing, it cannot have comments, views or likes
                return Post.From(postDocument);
            }
            return null;
        }
        else
        {
            await IncreaseViewsAsync(user.UserId, postId, contents, context);
        }

        return model;
    }

    private async Task<CommentDocument> GetCommentAsync(string userId, string postId, string commentId, OperationContext context)
    {
        var contents = _contentDb.GetContainer();
        var key = CommentDocument.Key(userId, postId, commentId);
        return await contents.ReadItemAsync<CommentDocument>(key.Id, new PartitionKey(key.Pk), _noResponseContent, context.Cancellation);
    }
    
    private static async Task ClearPendingCommentAsync(Container contents, PendingCommentsDocument pending, string postId, OperationContext context)
    {
        var index = pending.Items.Select((c, i) => (c, i)).First(x => x.c.PostId == postId).i;
        await contents.PatchItemAsync<PendingCommentsDocument>
        (
            pending.Id, new PartitionKey(pending.Pk),
            [PatchOperation.Remove($"/items/{index}")], // patch is case sensitive
            new PatchItemRequestOptions()
            {
                EnableContentResponseOnWrite = false,
                IfMatchEtag = pending.ETag
            },
            cancellationToken: context.Cancellation
        );
    }

    private static async Task<PendingCommentsDocument> RegisterPendingCommentAsync(Container contents, UserSession user, string parentUserId, string parentPostId, string postId, OperationContext context)
    {
        var pendingKey = PendingCommentsDocument.Key(user.UserId);
        var pendingComment = new PendingComment(user.UserId, postId, parentUserId, parentPostId, PendingCommentOperation.Add);
        var pendingCommentResponse = await contents.PatchItemAsync<PendingCommentsDocument>
        (
            pendingKey.Id, new PartitionKey(pendingKey.Pk),
            [PatchOperation.Add("/items/-", pendingComment)], // patch is case sensitive
            cancellationToken: context.Cancellation
        );
        var pending = pendingCommentResponse.Resource;
        pending.ETag = pendingCommentResponse.ETag;
        return pending;
    }
    
    private static async Task<PendingCommentsDocument> GetPendingCommentAsync(Container contents, string userId, OperationContext context)
    {
        var pendingKey = PendingCommentsDocument.Key(userId);
        var pendingCommentResponse = await contents.ReadItemAsync<PendingCommentsDocument>
        (
            pendingKey.Id, new PartitionKey(pendingKey.Pk),
            cancellationToken: context.Cancellation
        );
        var pending = pendingCommentResponse.Resource;
        pending.ETag = pendingCommentResponse.ETag;
        return pending;
    }

    private static async Task UpdateParentPostCommentCountsAsync(Container contents, string parentUserId, string parentPostId, OperationContext context)
    {
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
    }

    private static async Task<PostDocument> CreatePostAsync(Container contents, UserSession user, string? parentUserId, string? parentPostId, string postId, string content, OperationContext context)
    {
        var post = new PostDocument(user.UserId, postId, content, context.UtcNow.UtcDateTime, parentUserId, parentPostId);
        var postCounts = new PostCountsDocument(user.UserId, postId, 0, 0, 0, context.UtcNow.UtcDateTime, parentUserId, parentPostId);
        var batch = contents.CreateTransactionalBatch(new PartitionKey(post.Pk));
        batch.CreateItem(post, _noResponse);
        batch.CreateItem(postCounts, _noResponse);
        
        var response = await batch.ExecuteAsync(context.Cancellation);
        ThrowErrorIfTransactionFailed(ContentError.CreateCommentPostFailure, response);
        return post;
    }

    private static async Task CreateCommentInParentPostAsync(Container contents, UserSession user, string parentUserId, string parentPostId, string content, string postId, OperationContext context)
    {
        var comment = new CommentDocument(user.UserId, postId, parentUserId, parentPostId, content, context.UtcNow.UtcDateTime);
        var commentCounts = new CommentCountsDocument(user.UserId, postId, parentUserId, parentPostId, 0, 0, 0, context.UtcNow.UtcDateTime);
        var batch = contents.CreateTransactionalBatch(new PartitionKey(comment.Pk));
        batch.CreateItem(comment, _noResponse);
        batch.CreateItem(commentCounts, _noResponse);
        batch.PatchItem(PostCountsDocument.Key(parentUserId, parentPostId).Id, [PatchOperation.Increment( $"/{nameof(CommentCountsDocument.CommentCount)}", 1)], _noPatchResponse);
        
        var response = await batch.ExecuteAsync(context.Cancellation);
        ThrowErrorIfTransactionFailed(ContentError.CreateCommentFailure, response);
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

    private async Task<Post?> TryGetPostFromDbAsync(string userId, string postId, int lastCommentCount, OperationContext context, Container contents)
    {
        var keyFrom = PostDocument.KeyPostItemsStart(userId, postId);
        var keyTo = PostDocument.KeyPostItemsEnd(userId, postId);

        const string sql = "select * from u where u.pk = @pk and u.id >= @id and u.id < @id_end order by u.id desc offset 0 limit @limit";
        var query = new QueryDefinition(sql)
            .WithParameter("@pk", keyFrom.Pk)
            .WithParameter("@id", keyFrom.Id)
            .WithParameter("@id_end", keyTo.Id)
            .WithParameter("@limit", 2 + lastCommentCount * 2 );
        
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
    private static async Task IncreaseViewsAsync(string userId, string postId, Container contents, OperationContext context)
    {
        // TODO: Defer
        // Increase views
        var keyFrom = PostCountsDocument.Key(userId, postId);
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