using Microsoft.Azure.Cosmos;
using SocialApp.WebApi.Data._Shared;
using SocialApp.WebApi.Data.User;
using SocialApp.WebApi.Features._Shared.Services;

namespace SocialApp.WebApi.Features.Content.Containers;

public sealed class FeedContainer : CosmoContainer
{
    public FeedContainer(UserDatabase database)
        : base(database, "feeds")
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
}