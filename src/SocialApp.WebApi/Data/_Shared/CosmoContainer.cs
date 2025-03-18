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

    static readonly IDictionary<string, Type> _types = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);
    static CosmoContainer()
    {
        _types = AppDomain
            .CurrentDomain
            .GetAssemblies()
            .SelectMany(a => a.GetTypes())
            .Where(t => t.IsAssignableTo(typeof(Document)))
            .ToDictionary(x => x.Name, x => x);
    }

    protected Document? DeserializeDocument(JsonElement item)
    {
        var typeKey = item.GetProperty("type").GetString();
        if(string.IsNullOrWhiteSpace(typeKey))
            return null;
        
        if (_types.TryGetValue(typeKey, out var type))
        {
            var doc = _database.Deserialize(type, item) as Document;
            if (doc != null)
                doc.ETag = item.GetProperty("_etag").GetString();

            return doc;
        }

        return null;
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