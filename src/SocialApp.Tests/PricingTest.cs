using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using SocialApp.WebApi.Data._Shared;
using SocialApp.WebApi.Features._Shared.Services;

namespace SocialApp.Tests;

public class PricingTest
{
    private string _containerId = "pricing-test";
    private CosmosClient _cosmosClient;

    private record MyClass(string Pk, string Id, string Value, int Version = 0);
    
    [Test]
    public async Task TestPricing()
    {
        var container = _cosmosClient.GetContainer(_containerId, _containerId);
        var str = Guid.NewGuid().ToString().PadLeft(100,'N');
        var obj = new MyClass(Guid.NewGuid().ToString("N"), Guid.NewGuid().ToString("N"), str);
        
        Response<MyClass> response = await container.CreateItemAsync(obj);
        Console.WriteLine($"Single CreateItem: {response.RequestCharge}");
        
        response = await container.ReadItemAsync<MyClass>(obj.Id, new PartitionKey(obj.Pk));
        Console.WriteLine($"Single ReadItemAsync: {response.RequestCharge}");
        
        response = await container.UpsertItemAsync(obj with { Value = obj.Value+"!" });
        Console.WriteLine($"Single UpsertItemAsync: {response.RequestCharge}");
        
        response = await container.ReplaceItemAsync(obj with { Value = obj.Value+"!!"}, obj.Id);
        Console.WriteLine($"Single ReplaceItemAsync: {response.RequestCharge}");

        response = await container.PatchItemAsync<MyClass>(obj.Id, new PartitionKey(obj.Pk), [PatchOperation.Increment("/version", 1)]);
        Console.WriteLine($"Single PatchItemAsync: {response.RequestCharge}");
        
        response = await container.PatchItemAsync<MyClass>(obj.Id, new PartitionKey(obj.Pk), [PatchOperation.Increment("/version", 1), PatchOperation.Set("/value", obj.Value+"!!!")]);
        Console.WriteLine($"Single PatchItemAsync2: {response.RequestCharge}");
        
        response = await container.PatchItemAsync<MyClass>(obj.Id, new PartitionKey(obj.Pk), [PatchOperation.Set("/value", obj.Value+"!!!")]);
        Console.WriteLine($"Single PatchItemAsync3: {response.RequestCharge}");
        
        response = await container.DeleteItemAsync<MyClass>(obj.Id, new PartitionKey(obj.Pk));
        Console.WriteLine($"Single DeleteItemAsync: {response.RequestCharge}");

        obj = new MyClass(Guid.NewGuid().ToString("N"), Guid.NewGuid().ToString("N"), str);
        var batch = container.CreateTransactionalBatch(new PartitionKey(obj.Pk));
        batch.CreateItem(obj);
        var bresponse = await batch.ExecuteAsync();
        Console.WriteLine($"Batch CreateItem: {bresponse.RequestCharge}");
        
        batch = container.CreateTransactionalBatch(new PartitionKey(obj.Pk));
        batch.PatchItem(obj.Id, [PatchOperation.Set("/value", obj.Value+"!!!")]);
        bresponse = await batch.ExecuteAsync();
        Console.WriteLine($"Batch PatchItem : {bresponse.RequestCharge}");

        for (var i = 0; i < 10; i++)
        {
            batch = container.CreateTransactionalBatch(new PartitionKey(obj.Pk));
            for (int j = 0; j < 10; j++)
                batch.CreateItem(new MyClass(obj.Pk, Guid.NewGuid().ToString("N"), obj.Value + i + j));
            await batch.ExecuteAsync();
        }

        var query = new QueryDefinition("select * from c offset 0 limit 10");
        var iterator = container.GetItemQueryIterator<MyClass>(query);
        while (iterator.HasMoreResults)
        {
            var items = await iterator.ReadNextAsync();
            Console.WriteLine($"Query : {items.RequestCharge}");
            foreach (var item in items)
            {
                item.ToString();
            }
        }
    }
    
    [OneTimeSetUp]
    public async Task Setup()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.Local.json", optional: true)
            .Build();
        
        _cosmosClient = CosmoDatabase.CreateCosmosClient
        (
            configuration.GetSection("CosmosDb:User"),
            configuration.GetValue<string>("CosmosDb:ApplicationName") ?? throw new SocialAppConfigurationException("Missing CosmosDb ApplicationName")
        );
        
        Database database = await _cosmosClient.CreateDatabaseIfNotExistsAsync(_containerId);
        Console.WriteLine("Created Database: {0}\n", database.Id);
        var indexingPolicy = new IndexingPolicy
        {
            IndexingMode = IndexingMode.Consistent,
            Automatic = true,
            ExcludedPaths = { new ExcludedPath { Path = "/*" } },
            IncludedPaths = {  },
            CompositeIndexes = { }
        };
        
        Container container = await database.CreateContainerIfNotExistsAsync(new ContainerProperties
        {
            Id = _containerId,
            PartitionKeyPath = "/pk",
            IndexingPolicy = indexingPolicy,
            DefaultTimeToLive = -1,
            PartitionKeyDefinitionVersion = PartitionKeyDefinitionVersion.V2,
        });
        Console.WriteLine("Created Container: {0}\n", container.Id);
    }
    
    [OneTimeTearDown]
    public async Task TearDown()
    {
        await _cosmosClient.GetDatabase(_containerId).GetContainer(_containerId).DeleteContainerAsync();
        _cosmosClient.Dispose();
    }
}