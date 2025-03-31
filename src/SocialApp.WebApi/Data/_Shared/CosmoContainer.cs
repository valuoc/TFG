using System.Net;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Azure.Cosmos;
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

    private Document? DeserializeDocument(JsonElement item)
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
    
    protected async IAsyncEnumerable<Document> MultiQueryAsync(QueryDefinition query, OperationContext context)
    {
        using var itemIterator = Container.GetItemQueryIterator<JsonElement>(query, null, new QueryRequestOptions
        {
            PopulateIndexMetrics = true
        });

        while (itemIterator.HasMoreResults)
        {
            var items = await itemIterator.ReadNextAsync(context.Cancellation);
            context.AddRequestCharge(items.RequestCharge);
            context.SaveDebugMetrics(items.IndexMetrics);
            foreach (var item in items)
            {
                var document = DeserializeDocument(item);
                if (document == null)
                    throw new InvalidOperationException("Unable to deserialize document: " + item.ToString());
                yield return document;
            }
        }
    }
    
    public async Task<IReadOnlyList<string>> GetFeedRangesAsync()
    {
        var ranges = await Container.GetFeedRangesAsync();
        return ranges.Select(x => x.ToJsonString()).ToList();
    }
    
    public async IAsyncEnumerable<Document> ReadFeedAsync(string range, string? continuation, [EnumeratorCancellation] CancellationToken cancel)
    {
        var start = ChangeFeedStartFrom.Beginning(FeedRange.FromJsonString(range));
        if(continuation != null)
            start = ChangeFeedStartFrom.ContinuationToken(continuation);
        
        var iterator = Container.GetChangeFeedIterator<JsonElement>(start, ChangeFeedMode.LatestVersion);
        while (iterator.HasMoreResults)
        {
            var items = await iterator.ReadNextAsync(cancel);
            
            continuation = items.ContinuationToken;

            if (items.StatusCode == HttpStatusCode.NotModified)
            {
                await Task.Delay(TimeSpan.FromSeconds(1), cancel);
            }
            else
            {
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
}