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
    
    public async Task<(IReadOnlyList<FeedThreadDocument>, IReadOnlyList<FeedThreadCountsDocument>)> GetUserFeedDocumentsAsync(string userId, string? afterPostId, int limit, OperationContext context)
    {
        var keyStart = FeedThreadDocument.KeyUserFeedStart(userId);

        const string sql = @"
            select * 
            from c 
            where c.pk = @pk 
              and c.sk >= @start
              and c.sk < @end
              and not is_defined(c.deleted)
            order by c.sk desc 
            offset 0 limit @limit";
        
        var query = new QueryDefinition(sql)
            .WithParameter("@pk", keyStart.Pk)
            .WithParameter("@start", afterPostId == null ? keyStart.Id : FeedThreadDocument.KeyUserFeedFrom(userId, afterPostId).Id)
            .WithParameter("@end", FeedThreadDocument.KeyUserFeedEnd(userId).Id)
            .WithParameter("@limit", limit * 2); 
        
        var posts = new List<FeedThreadDocument>();
        var postCounts = new List<FeedThreadCountsDocument>();
        await foreach (var document in ExecuteQueryReaderAsync(query, keyStart.Pk, context))
        {
            if(document is FeedThreadDocument postDocument)
                posts.Add(postDocument);
            else if (document is FeedThreadCountsDocument postCountsDocument)
                postCounts.Add(postCountsDocument);
            else
                throw new InvalidOperationException("Unexpected document: " + document.GetType().Name);
        }
        
        return (posts, postCounts);
    }

    public async Task SaveFeedItemAsync(FeedThreadDocument feedItem, OperationContext context)
    {
        var response = await Container.UpsertItemAsync(feedItem, requestOptions: _noResponseContent, cancellationToken: context.Cancellation);
        context.AddRequestCharge(response.RequestCharge);
    }

    public async Task SaveFeedItemAsync(FeedThreadCountsDocument feedItem, OperationContext context)
    {
        var response = await Container.UpsertItemAsync(feedItem, requestOptions: _noResponseContent, cancellationToken: context.Cancellation);
        context.AddRequestCharge(response.RequestCharge);
    }
}