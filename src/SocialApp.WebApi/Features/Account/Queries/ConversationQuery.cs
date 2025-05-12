using Microsoft.Azure.Cosmos;
using SocialApp.WebApi.Data._Shared;
using SocialApp.WebApi.Data.User;
using SocialApp.WebApi.Features._Shared.Services;
using SocialApp.WebApi.Features._Shared.Tuples;

namespace SocialApp.WebApi.Features.Account.Queries;

public sealed class ConversationQuery : IQuerySingle<FullConversationTuple?>
{
    public string UserId {get; set;}
    public string ConversationId {get; set;}
    public int LastCommentCount {get; set;}
}

public sealed class ConversationQueryHandler : IQuerySingleHandler<ConversationQuery, FullConversationTuple?>
{
    public async Task<FullConversationTuple?> ExecuteQueryAsync(CosmoContainer container, ConversationQuery query, OperationContext context)
    {
        var keyFrom = ConversationDocument.KeyConversationsItemsStart(query.UserId, query.ConversationId);
        var keyTo = ConversationDocument.KeyConversationItemsEnd(query.UserId, query.ConversationId);

        const string sql = @"
            select * from c 
            where c.pk = @pk 
              and c.sk >= @id 
              and c.sk < @id_end
              and not is_defined(c.deleted) 
            order by c.sk desc 
            offset 0 limit @limit";
        
        var cosmosDb = new QueryDefinition(sql)
            .WithParameter("@pk", keyFrom.Pk)
            .WithParameter("@id", keyFrom.Id)
            .WithParameter("@id_end", keyTo.Id)
            .WithParameter("@limit", 2 + query.LastCommentCount * 2 );
        
        ConversationDocument? conversation = null;
        ConversationCountsDocument? conversationCounts = null;
        var comments = new List<CommentTuple>();
        CommentCountsDocument? commentCounts = null;
        await foreach (var document in container.ExecuteQueryReaderAsync(cosmosDb, keyFrom.Pk, context))
        {
            if(document is ConversationDocument conversationDocument)
            {
                conversation = conversation == null ? conversationDocument : throw new InvalidOperationException("Expecting a single conversation.");
            }
            else if(document is ConversationCountsDocument conversationCountsDocument)
            {
                conversationCounts = conversationCounts == null ? conversationCountsDocument : throw new InvalidOperationException("Expecting a single conversation.");
            }
            else if(document is CommentDocument commentDocument)
            {
                if(commentCounts == null)
                    throw new InvalidOperationException("Missing counts");
                if(commentCounts.ConversationId != commentDocument.ConversationId)
                    throw new InvalidOperationException("Different conversation");
                comments.Add(new CommentTuple(commentDocument, commentCounts));
                commentCounts = null;
            }
            else if(document is CommentCountsDocument commentCountsDocument)
            {
                commentCounts = commentCountsDocument;
            }
            else
                throw new InvalidOperationException("Unexpected document: " + document.GetType().Name);
        }

        if (conversation == null || conversation is { Deleted: true })
            return null;
        
        return new FullConversationTuple(new ConversationTuple( conversation, conversationCounts), comments);
    }
}