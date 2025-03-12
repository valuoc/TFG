using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using SocialApp.WebApi.Features.Account.Databases;
using SocialApp.WebApi.Features.Account.Services;
using SocialApp.WebApi.Features.Databases;
using SocialApp.WebApi.Features.Follow.Databases;
using SocialApp.WebApi.Features.Follow.Services;
using SocialApp.WebApi.Features.Session.Databases;
using SocialApp.WebApi.Features.Session.Services;

namespace SocialApp.Tests.ServicesTests;

public abstract class ServiceTestsBase
{
    protected AccountService AccountService;
    protected SessionService SessionService;
    protected FollowersService FollowersService;
    
    private AccountDatabase _accountDatabase;
    private ProfileDatabase _profileDatabase;
    private SessionDatabase _sessionDatabase;
    private FollowersDatabase _followerDatabase;
    
    private CosmosClient _cosmosClient;

    private readonly string _container = "user";
    private readonly string _databaseId = "cosmosdbpoc";
    
    [SetUp]
    public async Task Setup()
    {
        IConfiguration config = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.Local.json", optional: true)
            .Build();
        
        var endpoint = config.GetValue<string>("CosmosDb:Endpoint", null) ?? throw new InvalidOperationException("Missing CosmosDb Endpoint");
        var authKey = config.GetValue<string>("CosmosDb:AuthKey", null) ?? throw new InvalidOperationException("Missing CosmosDb AuthKey");
        var applicationName = config.GetValue<string>("CosmosDb:ApplicationName", null) ?? throw new InvalidOperationException("Missing CosmosDb ApplicationName");
        
        _cosmosClient = CosmoDatabase.CreateCosmosClient
        (
            endpoint, 
            authKey, 
            applicationName
        );
        
        _accountDatabase = new AccountDatabase(_cosmosClient, _databaseId, _container);
        _profileDatabase = new ProfileDatabase(_cosmosClient, _databaseId, _container);
        _sessionDatabase = new SessionDatabase(_cosmosClient, _databaseId, _container);
        _followerDatabase = new FollowersDatabase(_cosmosClient, _databaseId, _container);

        AccountService = new AccountService(_accountDatabase, _profileDatabase);
        SessionService = new SessionService(_sessionDatabase);
        FollowersService = new FollowersService(_followerDatabase);
        
        await _accountDatabase.InitializeAsync();
        await _profileDatabase.InitializeAsync();
        await _sessionDatabase.InitializeAsync();
        await _followerDatabase.InitializeAsync();
    }
    
    [TearDown]
    public async Task TearDown()
    {
        await _cosmosClient.GetDatabase(_databaseId).GetContainer(_container).DeleteContainerAsync();
        _cosmosClient.Dispose();
    }
}