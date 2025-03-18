using System.Text.Json;
using Microsoft.Azure.Cosmos;
using SocialApp.WebApi.Data.User;
using SocialApp.WebApi.Features._Shared.Services;

namespace SocialApp.WebApi.Data._Shared;

public abstract class CosmoContainer
{
    protected readonly Container Container;
    private readonly CosmoDatabase _database;

    protected CosmoContainer(CosmoDatabase database)
    {
        Container = database.GetContainer();
        _database = database;
    }

    protected Document? DeserializeDocument(JsonElement item)
    {
        var type = item.GetProperty("type").GetString();
        Document? doc = type switch
        {
            nameof(PostDocument) => _database.Deserialize<PostDocument>(item),
            nameof(CommentDocument) => _database.Deserialize<CommentDocument>(item),
            nameof(PostCountsDocument) => _database.Deserialize<PostCountsDocument>(item),
            nameof(CommentCountsDocument) => _database.Deserialize<CommentCountsDocument>(item),
            _ => null
        };
        if (doc != null)
            doc.ETag = item.GetProperty("_etag").GetString();

        return doc;
    }
    
    protected async IAsyncEnumerable<Document> MultiQueryAsync(QueryDefinition postQuery, OperationContext context)
    {
        using var itemIterator = Container.GetItemQueryIterator<JsonElement>(postQuery);

        while (itemIterator.HasMoreResults)
        {
            var items = await itemIterator.ReadNextAsync(context.Cancellation);
            foreach (var item in items)
            {
                var document = DeserializeDocument(item);
                if (document == null)
                    throw new InvalidOperationException("Unable to deserialize document: " + item.ToString());
                yield return document;
            }
        }
    }
}