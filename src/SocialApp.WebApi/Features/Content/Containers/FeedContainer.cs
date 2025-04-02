using Microsoft.Azure.Cosmos;
using SocialApp.WebApi.Data._Shared;
using SocialApp.WebApi.Data.User;
using SocialApp.WebApi.Features._Shared.Services;

namespace SocialApp.WebApi.Features.Content.Containers;

public sealed class FeedContainer : CosmoContainer
{
    private static readonly TransactionalBatchPatchItemRequestOptions _noPatchResponse = new() {EnableContentResponseOnWrite = false};
    private static readonly ItemRequestOptions _noResponseContent = new(){ EnableContentResponseOnWrite = false};
    private static readonly TransactionalBatchItemRequestOptions _noResponse = new() { EnableContentResponseOnWrite = false };
    private static readonly PatchItemRequestOptions _patchItemNoResponse = new() { EnableContentResponseOnWrite = false};
    
    public FeedContainer(UserDatabase database)
        : base(database)
    { }
    
    public async ValueTask<IReadOnlyList<(FeedPostDocument, FeedPostCountsDocument)>> GetUserFeedAsync(string userId, string? afterPostId, int limit, OperationContext context)
    {
        var key = FeedPostDocument.KeyUserPostsEnd(userId);

        const string sql = @"
            select * 
            from c 
            where c.pk = @pk 
              and c.isFeed = true
              and c.sk < @id 
            order by c.sk desc 
            offset 0 limit @limit";
        
        var query = new QueryDefinition(sql)
            .WithParameter("@pk", key.Pk)
            .WithParameter("@id", afterPostId == null ? key.Id : PostDocument.Key(userId, afterPostId).Id)
            .WithParameter("@limit", limit * 2);
        
        var posts = new List<FeedPostDocument>();
        var postCounts = new List<FeedPostCountsDocument>();
        await foreach (var document in MultiQueryAsync(query, context))
        {
            if(document is FeedPostDocument postDocument)
                posts.Add(postDocument);
            else if (document is FeedPostCountsDocument postCountsDocument)
                postCounts.Add(postCountsDocument);
            else
                throw new InvalidOperationException("Unexpected document: " + document.GetType().Name);
        }
        
        return posts.Zip(postCounts).Select(x => (x.First, x.Second)).ToList();
    }

    public async ValueTask SaveFeedItemAsync(FeedPostDocument feedItem, CancellationToken cancel)
        => await Container.UpsertItemAsync(feedItem, requestOptions: _noResponseContent, cancellationToken: cancel);

    public async ValueTask SaveFeedItemAsync(FeedPostCountsDocument feedItem, CancellationToken cancel)
        => await Container.UpsertItemAsync(feedItem, requestOptions: _noResponseContent, cancellationToken: cancel);
}