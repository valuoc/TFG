using Microsoft.Azure.Cosmos;
using SocialApp.WebApi.Data._Shared;
using SocialApp.WebApi.Data.User;
using SocialApp.WebApi.Features._Shared.Services;
using SocialApp.WebApi.Features._Shared.Tuples;

namespace SocialApp.WebApi.Features.Content.Queries;

public sealed class PreviousCommentsQuery : IQueryMany<CommentTuple>
{
    public string UserId { get; set; }
    public string ConversationId { get; set; }
    public string CommentId { get; set; }
    public int LastCommentCount { get; set; }
}

public sealed class PreviousCommentsQueryHandler : IQueryManyHandler<PreviousCommentsQuery, CommentTuple>
{
    public async IAsyncEnumerable<CommentTuple> ExecuteQueryAsync(CosmoContainer container, PreviousCommentsQuery query, OperationContext context)
    {
        var key = CommentDocument.Key(query.UserId, query.ConversationId, query.CommentId);
        var keyTo = ConversationDocument.KeyConversationsItemsStart(query.UserId, query.ConversationId);

        const string sql = @"
            select * from c 
               where c.pk = @pk 
                and c.sk < @id 
                and c.sk > @id_end 
                and not is_defined(c.deleted)
              order by c.sk desc offset 0 limit @limit";
        
        var cosmosDb = new QueryDefinition(sql)
            .WithParameter("@pk", key.Pk)
            .WithParameter("@id", key.Id)
            .WithParameter("@id_end", keyTo.Id)
            .WithParameter("@limit", query.LastCommentCount * 2);
        
        CommentCountsDocument? counts = null;
        await foreach (var document in container.ExecuteQueryReaderAsync(cosmosDb, key.Pk, context))
        {
            if (document is CommentDocument doc)
            {
                if(counts == null)
                    throw new InvalidOperationException("Missing counts");
                if(counts.ConversationId != doc.ConversationId)
                    throw new InvalidOperationException("Different conversation");
                yield return new CommentTuple(doc, counts);
                counts = null;
            }
            else if(document is CommentCountsDocument commentCountsDocument)
                counts = commentCountsDocument;
            else
                throw new InvalidOperationException("Unexpected document: " + document.GetType().Name);
        }
    }
}