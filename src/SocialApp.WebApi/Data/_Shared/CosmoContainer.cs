using System.Net;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Azure.Cosmos;
using SocialApp.WebApi.Features._Shared.Services;
using SocialApp.WebApi.Features.Content.Services;

namespace SocialApp.WebApi.Data._Shared;

public abstract class CosmoContainer
{
    protected readonly Container Container;
    
    protected CosmoContainer(CosmoDatabase database, string name)
    {
        Container = database.GetContainer(name);
    }
    
    public async Task<T?> GetAsync<T>(DocumentKey key, OperationContext context)
        where T:Document
    {
        try
        {
            var response = await Container.ReadItemAsync<T>(key.Id, new PartitionKey(key.Pk), cancellationToken:context.Cancellation);
            context.AddRequestCharge(response.RequestCharge);
            return response.Resource;
        }
        catch (CosmosException e) when (e.StatusCode == HttpStatusCode.NotFound)
        {
            context.AddRequestCharge(e.RequestCharge);
            return null;
        }
    }
    
    public async Task<IReadOnlyDictionary<DocumentKey,T>> GetManyAsync<T>(IEnumerable<DocumentKey> keys, OperationContext context)
        where T:Document
    {
        try
        {
            var response = await Container.ReadManyItemsAsync<T>(keys.Select(k => (k.Id, new PartitionKey(k.Pk))).ToList(), cancellationToken: context.Cancellation);
            context.AddRequestCharge(response.RequestCharge);

            return response.Resource.ToDictionary(x => new DocumentKey(x.Pk, x.Id), x => x);
        }
        catch (CosmosException e)
        {
            context.AddRequestCharge(e.RequestCharge);
            throw;
        }
    }

    public IUnitOfWork CreateUnitOfWork(string pk)
    {
        return new CosmosUnitOfWork(Container.CreateTransactionalBatch(new PartitionKey(pk)));
    }
    
    public async IAsyncEnumerable<Document> ExecuteQueryReaderAsync(QueryDefinition query, string partitionKey, OperationContext context)
    {
        using var itemIterator = Container.GetItemQueryIterator<JsonElement>(query, null, new QueryRequestOptions
        {
            PopulateIndexMetrics = true,
            EnableScanInQuery = false,
            PartitionKey = new PartitionKey(partitionKey),
            EnableOptimisticDirectExecution = true,
            MaxItemCount = 1_000 // ??
        });

        while (itemIterator.HasMoreResults)
        {
            var items = await itemIterator.ReadNextAsync(context.Cancellation);
            context.AddRequestCharge(items.RequestCharge);
            context.SaveDebugMetrics(items.IndexMetrics);
            context.SaveQueryMetrics(items.Diagnostics.GetQueryMetrics());
            
            foreach (var item in items)
            {
                var document = DocumentSerialization.DeserializeDocument(item);
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
    
    public async IAsyncEnumerable<(IReadOnlyList<Document> items, string continuation)> ReadFeedAsync(string range, string? continuation, [EnumeratorCancellation] CancellationToken cancel)
    {
        var start = ChangeFeedStartFrom.Beginning(FeedRange.FromJsonString(range));
        if(continuation != null)
            start = ChangeFeedStartFrom.ContinuationToken(continuation);
        
        var iterator = Container.GetChangeFeedIterator<JsonElement>(start, ChangeFeedMode.LatestVersion, new ChangeFeedRequestOptions
        {
            PageSizeHint = Environment.ProcessorCount * 2
        });
        while (iterator.HasMoreResults)
        {
            var items = await iterator.ReadNextAsync(cancel);
            
            continuation = items.ContinuationToken;

            if (items.StatusCode == HttpStatusCode.NotModified)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(750), cancel);
            }
            else
            {
                var documents = new Document[items.Count];
                for (var i = 0; i < items.Count; i++)
                {
                    var item = items.ElementAt(i);
                    var document = DocumentSerialization.DeserializeDocument(item);
                    if (document == null)
                        throw new InvalidOperationException("Unable to deserialize document: " + item);
                    documents[i] = document;
                }
                yield return (documents, continuation);
            }
        }
    }
    
    public async IAsyncEnumerable<ConflictResolutionResult> ProcessConflictFeedAsync(IConflictMerger merger, CancellationToken cancel)
    {
        var conflictFeed = Container.Conflicts.GetConflictQueryIterator<ConflictProperties>();
        while (conflictFeed.HasMoreResults)
        {
            var conflicts = await conflictFeed.ReadNextAsync(cancel);
            var context = new OperationContext(cancel);
            foreach (var conflict in conflicts)
            {
                var conflictingJson = Container.Conflicts.ReadConflictContent<JsonElement>(conflict);
                var remoteConflict = DocumentSerialization.DeserializeDocument(conflictingJson);
                var remoteKey = remoteConflict != null
                    ? new DocumentKey(remoteConflict.Pk, remoteConflict.Id)
                    : DocumentSerialization.DeserializeKey(conflictingJson);

                Document? localConflict = null;
                if (!remoteKey.HasValue)
                {
                    yield return new ConflictResolutionResult(new DocumentKey("unknown", conflict.Id), false);
                    continue;
                }
                
                var currentItem = await Container.Conflicts.ReadCurrentAsync<JsonElement>(conflict, new PartitionKey(remoteKey.Value.Pk), cancel);
                localConflict = DocumentSerialization.DeserializeDocument(currentItem);

                if(await merger.MergeAsync(remoteConflict, localConflict, context))
                {
                    // Delete the conflict
                    await Container.Conflicts.DeleteAsync(conflict, new PartitionKey(remoteKey.Value.Pk), cancel);
                    yield return new ConflictResolutionResult(remoteKey.Value, true);
                }
                else
                {
                    yield return new ConflictResolutionResult(remoteKey.Value, false);
                }
            }
        }
    }
}