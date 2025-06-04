using System.Collections.ObjectModel;
using Microsoft.Azure.Cosmos;
using SocialApp.WebApi.Features._Shared.Services;

namespace SocialApp.WebApi.Data._Shared;

public abstract class CosmoDatabase
{
    private readonly IConfiguration _configuration;
    protected readonly string DatabaseId;
    
    protected CosmosClient CosmosClient { get; }
    
    protected CosmoDatabase(CosmosClient cosmosClient, IConfiguration configuration)
    {
        _configuration = configuration;
        CosmosClient = cosmosClient;
        DatabaseId = configuration.GetValue<string>("Id") ?? throw new SocialAppConfigurationException("Missing CosmosDb Database Id");
    }
    
    private IEnumerable<KeyValuePair<string,string>> GetContainerIds()
        => _configuration
            .GetSection("Containers")
            .GetChildren()
            .Select(x => new KeyValuePair<string,string>(x.Key, x.GetValue<string>("Id") ?? throw new InvalidOperationException($"Missing Container Id in '{x.Key}'.")));

    public virtual async Task InitializeAsync()
    {
        Database database = await CosmosClient.CreateDatabaseIfNotExistsAsync(DatabaseId);
        Console.WriteLine("Created Database: {0}\n", database.Id);
        var indexingPolicy = new IndexingPolicy
        {
            IndexingMode = IndexingMode.Consistent,
            Automatic = true,
            ExcludedPaths = { new ExcludedPath { Path = "/*" } },
            IncludedPaths = {},
            CompositeIndexes = { }
        };
        foreach(var includedPath in GetIndexedPaths())
            indexingPolicy.IncludedPaths.Add(includedPath);
        
        foreach(var compositeIndex in GetCompositeIndexes())
            indexingPolicy.CompositeIndexes.Add(compositeIndex);
        
        foreach (var kv in GetContainerIds())
        {
            Container container = await database.CreateContainerIfNotExistsAsync(new ContainerProperties
            {
                Id = kv.Value,
                PartitionKeyPath = "/pk",
                IndexingPolicy = indexingPolicy,
                DefaultTimeToLive = -1,
                PartitionKeyDefinitionVersion = PartitionKeyDefinitionVersion.V2,
                ConflictResolutionPolicy = GetConflictResolutionPolicy()
            });
            Console.WriteLine("Created Container: {0}\n", container.Id);
        }
    }

    protected virtual ConflictResolutionPolicy GetConflictResolutionPolicy()
        => new()
        {
            Mode = ConflictResolutionMode.LastWriterWins,
            ResolutionPath = "/_ts"
        };

    protected virtual IEnumerable<Collection<CompositePath>> GetCompositeIndexes()
    {
        yield break;
    }

    protected virtual IEnumerable<IncludedPath> GetIndexedPaths()
    {
        yield break;
    }

    public Container GetContainer(string name)
    {
        var id = _configuration.GetValue<string>($"Containers:{name}:Id", string.Empty);
        if(string.IsNullOrWhiteSpace(id))
            throw new InvalidOperationException($"There is no configured container named '{name}' in '{DatabaseId}'.");
        return CosmosClient.GetContainer(DatabaseId, id);
    }
    
    public static CosmosClient CreateCosmosClient(IConfiguration configuration, string applicationName)
        => new(
            configuration.GetValue<string>("Endpoint") ?? throw new SocialAppConfigurationException("Missing CosmosDb Endpoint"), 
            configuration.GetValue<string>("AuthKey") ?? throw new SocialAppConfigurationException("Missing CosmosDb AuthKey"), 
            new CosmosClientOptions
            {
                ConnectionMode = ConnectionMode.Direct,
                ApplicationName = applicationName,
                UseSystemTextJsonSerializerWithOptions = DocumentSerialization.Options,
                ConsistencyLevel = null, // use account's level
                ApplicationRegion = null, // use first writable. The primary.
                //ApplicationPreferredRegions = 
            }
        );
}