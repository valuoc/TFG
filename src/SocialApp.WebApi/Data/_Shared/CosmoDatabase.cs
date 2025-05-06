using System.Collections.ObjectModel;
using Microsoft.Azure.Cosmos;
using SocialApp.WebApi.Features._Shared.Services;

namespace SocialApp.WebApi.Data._Shared;

public abstract class CosmoDatabase
{
    private readonly IConfiguration _configuration;
    private readonly Dictionary<string, string> _containerIdByName = new(StringComparer.OrdinalIgnoreCase);
    protected readonly string DatabaseId;
    
    protected CosmosClient CosmosClient { get; }
    
    protected CosmoDatabase(CosmosClient cosmosClient, IConfiguration configuration)
    {
        _configuration = configuration;
        CosmosClient = cosmosClient;
        DatabaseId = configuration.GetValue<string>("Id") ?? throw new SocialAppConfigurationException("Missing CosmosDb Database Id");
        var containerIds = _configuration
            .GetSection("Containers")
            .GetChildren()
            .Select(x => (x.Key, x.GetValue<string>("Id") ?? throw new InvalidOperationException($"Missing Container Id in '{x.Key}'.")))
            .ToArray();

        foreach (var (name, id) in containerIds)
            _containerIdByName[name] = id;
    }

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
        
        foreach (var kv in _containerIdByName)
        {
            Container container = await database.CreateContainerIfNotExistsAsync(new ContainerProperties
            {
                Id = kv.Value,
                PartitionKeyPath = "/pk",
                IndexingPolicy = indexingPolicy,
                DefaultTimeToLive = -1,
                PartitionKeyDefinitionVersion = PartitionKeyDefinitionVersion.V2,
            });
            Console.WriteLine("Created Container: {0}\n", container.Id);
        }
    }

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
        if (!_containerIdByName.TryGetValue(name, out var id))
            throw new InvalidOperationException($"There is no configured container named '{name}'.");
            
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