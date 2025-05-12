using Microsoft.Azure.Cosmos;
using SocialApp.WebApi.Data._Shared;
using SocialApp.WebApi.Data.User;
using SocialApp.WebApi.Features._Shared.Services;
using SocialApp.WebApi.Features._Shared.Tuples;

namespace SocialApp.WebApi.Features.Account.Queries;

public sealed class UserConversationsQuery : IQueryMany<ConversationTuple>
{
    public string UserId { get; set; }
    public string? BeforeConversationId  { get; set; } 
    public int Limit { get; set; }
}

public sealed class UserConversationsQueryHandler : IQueryManyHandler<UserConversationsQuery, ConversationTuple>
{
    public async IAsyncEnumerable<ConversationTuple> ExecuteQueryAsync(CosmoContainer container, UserConversationsQuery query, OperationContext context)
    {
        var key = ConversationDocument.KeyUserConversationsEnd(query.UserId);

        const string sql = @"
            select * 
            from c 
            where c.pk = @pk 
              and c.isRootConversation = true
              and c.sk < @id 
              and not is_defined(c.parentConversationUserId)
              and not is_defined(c.deleted)
            order by c.sk desc 
            offset 0 limit @limit";
        
        var cosmosDb = new QueryDefinition(sql)
            .WithParameter("@pk", key.Pk)
            .WithParameter("@id", query.BeforeConversationId == null ? key.Id : ConversationDocument.Key(query.UserId, query.BeforeConversationId).Id)
            .WithParameter("@limit", query.Limit * 2);
        
        ConversationCountsDocument? counts = null;
        await foreach (var document in container.ExecuteQueryReaderAsync(cosmosDb, key.Pk, context))
        {
            if(document is ConversationDocument doc)
            {
                if(counts == null)
                    throw new InvalidOperationException("Missing counts");
                if(counts.ConversationId != doc.ConversationId)
                    throw new InvalidOperationException("Different conversation");
                yield return new ConversationTuple(doc, counts);
                counts = null;
            }
            else if (document is ConversationCountsDocument docCount)
            {
                counts = docCount;
            }
            else
                throw new InvalidOperationException("Unexpected document: " + document.GetType().Name);
        }
    }
}

