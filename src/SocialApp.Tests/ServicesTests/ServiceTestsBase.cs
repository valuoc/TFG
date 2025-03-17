using System.Net;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using SocialApp.WebApi.Features.Account.Databases;
using SocialApp.WebApi.Features.Account.Services;
using SocialApp.WebApi.Features.Content.Databases;
using SocialApp.WebApi.Features.Content.Services;
using SocialApp.WebApi.Features.Databases;
using SocialApp.WebApi.Features.Follow.Services;
using SocialApp.WebApi.Features.Services;
using SocialApp.WebApi.Features.Session.Models;
using SocialApp.WebApi.Features.Session.Services;

namespace SocialApp.Tests.ServicesTests;

public abstract class ServiceTestsBase
{
    protected bool RemoveContainerAfterTests = true;
    
    protected AccountService AccountService;
    protected SessionService SessionService;
    protected FollowersService FollowersService;
    protected ContentService ContentService;
    
    private AccountDatabase _accountDatabase;
    private UserDatabase _userDatabase;
    private SessionDatabase _sessionDatabase;
    private ContentDatabase _contentDatabase;
    
    private CosmosClient _cosmosClient;

    private readonly string _container = "test";
    private readonly string _databaseId = "socialapp";
    
    
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
        _userDatabase = new UserDatabase(_cosmosClient, _databaseId, _container);
        _sessionDatabase = new SessionDatabase(_cosmosClient, _databaseId, _container);
        _contentDatabase = new ContentDatabase(_cosmosClient, _databaseId, _container);

        AccountService = new AccountService(_accountDatabase, _userDatabase, _sessionDatabase);
        SessionService = new SessionService(_userDatabase, _sessionDatabase);
        FollowersService = new FollowersService(_userDatabase);
        ContentService = new ContentService(_contentDatabase);
        
        // Content indexes /pk, /id and /type
        await _contentDatabase.InitializeAsync();
    }
    
    [TearDown]
    public async Task TearDown()
    {
        if(RemoveContainerAfterTests)
            await _cosmosClient.GetDatabase(_databaseId).GetContainer(_container).DeleteContainerAsync();
        _cosmosClient.Dispose();
    }
    
    protected async Task<UserSession> CreateUserAsync()
    {
        var userName = Guid.NewGuid().ToString("N");
        await AccountService.RegisterAsync($"{userName}@xxx.com", userName, "Display"+userName, "pass", OperationContext.None());
        var session = await SessionService.LoginWithPasswordAsync($"{userName}@xxx.com", "pass", OperationContext.None());
        return session ?? throw new InvalidOperationException("Cannot find user");
    }
    
    protected static CosmosException CreateCosmoException(HttpStatusCode code = HttpStatusCode.InternalServerError)
        => new(code.ToString(), code, (int)code, Guid.NewGuid().ToString(), 2);
}