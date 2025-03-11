using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Azure.Cosmos;

namespace SocialApp.WebApi.Features.Databases;

public abstract class CosmoDatabase
{
    protected CosmosClient CosmosClient { get; }
    protected readonly string DatabaseId;
    protected readonly string ContainerId;

    protected CosmoDatabase(CosmosClient cosmosClient, string databaseId, string containerId)
    {
        CosmosClient = cosmosClient;
        DatabaseId = databaseId;
        ContainerId = containerId;
    }

    public virtual async ValueTask InitializeAsync()
    {
        Database database = await CosmosClient.CreateDatabaseIfNotExistsAsync(DatabaseId);
        Console.WriteLine("Created Database: {0}\n", database.Id);
        Container container = await database.CreateContainerIfNotExistsAsync(new ContainerProperties
        {
            Id = ContainerId,
            PartitionKeyPath = "/pk",
            IndexingPolicy = new IndexingPolicy()
            {
                IndexingMode = IndexingMode.Consistent,
                Automatic = true,
                ExcludedPaths = { new ExcludedPath() { Path = "/*" } },
                IncludedPaths = { },
            },
            DefaultTimeToLive = -1,
            PartitionKeyDefinitionVersion = PartitionKeyDefinitionVersion.V2,
        });
        Console.WriteLine("Created Container: {0}\n", container.Id);
    }
    
    public Container GetContainer()
    {
        return CosmosClient.GetContainer(DatabaseId, ContainerId);
    }
    
    private static JsonSerializerOptions CreateJsonSerializerOptions()
        => new()
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.Never,
            WriteIndented = true,
            PropertyNameCaseInsensitive = true, 
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    
    public static CosmosClient CreateCosmosClient(string endpoint, string authKey, string applicationName)
        => new(
            endpoint, 
            authKey, 
            new CosmosClientOptions()
            {
                ConnectionMode = ConnectionMode.Direct,
                ApplicationName = applicationName,
                UseSystemTextJsonSerializerWithOptions = CreateJsonSerializerOptions(),
                ConsistencyLevel = null, // use account's level
                ApplicationRegion = null, // use first writable. The primary.
                //ApplicationPreferredRegions = 
            }
        );
}