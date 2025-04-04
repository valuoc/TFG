using Microsoft.Azure.Cosmos;
using SocialApp.WebApi.Data.Shared;
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
    
    public async Task<(IReadOnlyList<FeedConversationDocument>, IReadOnlyList<FeedConversationCountsDocument>)> GetUserFeedDocumentsAsync(string userId, string? beforeConversationId, int limit, OperationContext context)
    {
        // ascending order would mean that the oldest ones come first and the most recent ones last.
        
        var keyStart = FeedConversationDocument.KeyUserFeedStart(userId);

        const string sql = @"
            select * 
            from c 
            where c.pk = @pk 
              and c.sk > @start
              and c.sk < @end
              and not is_defined(c.deleted)
            order by c.sk desc 
            offset 0 limit @limit";
        
        var query = new QueryDefinition(sql)
            .WithParameter("@pk", keyStart.Pk)
            .WithParameter("@start", keyStart.Id)
            .WithParameter("@end", beforeConversationId == null ? FeedConversationDocument.KeyUserFeedEnd(userId).Id : FeedConversationDocument.KeyUserFeedFrom(userId, beforeConversationId).Id)
            .WithParameter("@limit", limit * 2); 
        
        var conversations = new List<FeedConversationDocument>();
        var conversationCounts = new List<FeedConversationCountsDocument>();
        await foreach (var document in ExecuteQueryReaderAsync(query, keyStart.Pk, context))
        {
            if(document is FeedConversationDocument conversationDocument)
                conversations.Add(conversationDocument);
            else if (document is FeedConversationCountsDocument conversationCountsDocument)
                conversationCounts.Add(conversationCountsDocument);
            else
                throw new InvalidOperationException("Unexpected document: " + document.GetType().Name);
        }
        
        return (conversations, conversationCounts);
    }

    public async Task SaveFeedItemAsync(FeedConversationDocument feedItem, OperationContext context)
    {
        var response = await Container.UpsertItemAsync(feedItem, requestOptions: _noResponseContent, cancellationToken: context.Cancellation);
        context.AddRequestCharge(response.RequestCharge);
    }

    public async Task SaveFeedItemAsync(FeedConversationCountsDocument feedItem, OperationContext context)
    {
        var response = await Container.UpsertItemAsync(feedItem, requestOptions: _noResponseContent, cancellationToken: context.Cancellation);
        context.AddRequestCharge(response.RequestCharge);
    }
}