using System.Text.Json;
using Microsoft.Azure.Cosmos;
using SocialApp.WebApi.Features.Content.Databases;
using SocialApp.WebApi.Features.Content.Documents;
using SocialApp.WebApi.Features.Content.Models;
using SocialApp.WebApi.Features.Services;
using SocialApp.WebApi.Features.Session.Models;

namespace SocialApp.WebApi.Features.Content.Services;

public sealed class ContentService
{
    private static readonly ItemRequestOptions _noResponseContent = new(){ EnableContentResponseOnWrite = false};
    private static readonly TransactionalBatchItemRequestOptions _noResponse = new TransactionalBatchItemRequestOptions() { EnableContentResponseOnWrite = false };
    
    private readonly ContentDatabase _contentDb;

    public ContentService(ContentDatabase contentDb)
    {
        _contentDb = contentDb;
    }

    public async ValueTask<string> CreatePostAsync(UserSession user, string content, OperationContext context)
    {
        var post = new PostDocument(user.UserId, Ulid.NewUlid().ToString(), content, null, null);
        var postCounts = new PostCountsDocument(user.UserId, post.PostId, 0, 0, 1, null, null);
        var contents = _contentDb.GetContainer();
        var batch = contents.CreateTransactionalBatch(new PartitionKey(post.Pk));
        batch.CreateItem(post);
        batch.CreateItem(postCounts);
        await batch.ExecuteAsync(context.Cancellation);
        return post.PostId;
    }
    
    public async ValueTask<string> CommentAsync(UserSession user, string parentUserId, string parentPostId, string content, OperationContext context)
    {
        var post = new PostDocument(user.UserId, Ulid.NewUlid(context.UtcNow).ToString(), content, parentUserId, parentPostId);
        var postCounts = new PostCountsDocument(user.UserId, post.PostId, 0, 0, 1, parentUserId, parentPostId);
        var contents = _contentDb.GetContainer();
        var batch = contents.CreateTransactionalBatch(new PartitionKey(post.Pk));
        batch.CreateItem(post, _noResponse);
        batch.CreateItem(postCounts, _noResponse);
        await batch.ExecuteAsync(context.Cancellation);
        
        var comment = new CommentDocument(user.UserId, post.PostId, parentUserId, parentPostId, content);
        var commentCounts = new CommentCountsDocument(user.UserId, post.PostId, parentUserId, parentPostId, 0, 0, 1);
        batch = contents.CreateTransactionalBatch(new PartitionKey(comment.Pk));
        batch.CreateItem(comment, _noResponse);
        batch.CreateItem(commentCounts, _noResponse);
        await batch.ExecuteAsync(context.Cancellation);
        
        return post.PostId;
    }

    private object? Discriminate(JsonElement item)
    {
        var type = item.GetProperty("type").GetString();
        return type switch
        {
            nameof(PostDocument) => _contentDb.Deserialize<PostDocument>(item),
            nameof(CommentDocument) => _contentDb.Deserialize<CommentDocument>(item),
            nameof(PostCountsDocument) => _contentDb.Deserialize<PostCountsDocument>(item),
            _ => null
        };
    }

    public async ValueTask<PostWithComments?> GetPostAsync(string userId, string postId, OperationContext context)
    {
        var key = PostDocument.Key(userId, postId);
        var keyLimit = PostDocument.KeyLimit(userId, postId);
        var contents = _contentDb.GetContainer();

        var query = new QueryDefinition("select * from u where u.pk = @pk and u.id >= @id and u.id < @id_end")
            .WithParameter("@pk", key.Pk)
            .WithParameter("@id", key.Id)
            .WithParameter("@id_end", keyLimit.Id);

        PostWithComments? model = null;
        
        using var itemIterator = contents.GetItemQueryIterator<JsonElement>(query);

        while (itemIterator.HasMoreResults)
        {
            var items = await itemIterator.ReadNextAsync(context.Cancellation);
            foreach (var item in items)
            {
                var document = Discriminate(item);

                switch (document)
                {
                    case PostDocument post:
                        model = PostWithComments.From(post);
                        break;
                    case PostCountsDocument counts:
                        model.ViewCount = counts.ViewCount;
                        model.CommentCount = counts.CommentCount;
                        model.LikeCount = counts.LikeCount;
                        break;
                    case CommentDocument comment:
                        model.Comments.Add(PostComment.From(comment));
                        break;
                }
            }
        }

        return model;
    }

    public ValueTask<IReadOnlyList<string>> GetAllPostsAsync(string userId, OperationContext context)
    {
        return ValueTask.FromResult((IReadOnlyList<string>)new List<string>().AsReadOnly());
    }
}