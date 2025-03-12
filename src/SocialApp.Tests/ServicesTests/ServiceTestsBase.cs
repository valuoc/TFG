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
        
        _accountDatabase = new AccountDatabase(_cosmosClient, "cosmosdbpoc", "user");
        _profileDatabase = new ProfileDatabase(_cosmosClient, "cosmosdbpoc", "user");
        _sessionDatabase = new SessionDatabase(_cosmosClient, "cosmosdbpoc", "user");
        _followerDatabase = new FollowersDatabase(_cosmosClient, "cosmosdbpoc", "user");

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
        _cosmosClient.Dispose();
    }
}