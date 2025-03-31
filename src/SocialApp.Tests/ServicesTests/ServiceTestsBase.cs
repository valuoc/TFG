using System.Net;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using SocialApp.WebApi.Data._Shared;
using SocialApp.WebApi.Data.Account;
using SocialApp.WebApi.Data.Session;
using SocialApp.WebApi.Data.User;
using SocialApp.WebApi.Features._Shared.Services;
using SocialApp.WebApi.Features.Account.Services;
using SocialApp.WebApi.Features.Content.Services;
using SocialApp.WebApi.Features.Follow.Services;
using SocialApp.WebApi.Features.Session.Models;
using SocialApp.WebApi.Features.Session.Services;

namespace SocialApp.Tests.ServicesTests;

public abstract class ServiceTestsBase
{
    protected bool RemoveContainerAfterTests = false;
    
    protected AccountService AccountService;
    protected SessionService SessionService;
    protected FollowersService FollowersService;
    protected ContentService ContentService;
    
    private AccountDatabase _accountDatabase;
    private UserDatabase _userDatabase;
    private SessionDatabase _sessionDatabase;
    
    private CosmosClient _cosmosClient;

    private readonly string _container = "test";
    private readonly string _databaseId = "socialapp";
    
    private readonly CancellationTokenSource _changeFeedCancellationToken = new CancellationTokenSource();
    
    [OneTimeSetUp]
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

        AccountService = new AccountService(_accountDatabase, _userDatabase, _sessionDatabase);
        SessionService = new SessionService(_userDatabase, _sessionDatabase);
        FollowersService = new FollowersService(_userDatabase);
        ContentService = new ContentService(_userDatabase);
        
        // Content indexes /pk, /id and /type
        await _userDatabase.InitializeAsync();
        
        _ = Task.Run(() =>ContentService.ProcessChangeFeedAsync(_changeFeedCancellationToken.Token));
    }
    
    [OneTimeTearDown]
    public async Task TearDown()
    {
        await _changeFeedCancellationToken.CancelAsync();
        _changeFeedCancellationToken.Dispose();
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