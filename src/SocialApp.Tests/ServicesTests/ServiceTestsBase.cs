using System.Net;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
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
    protected FeedService FeedService;
    protected ContentStreamProcessorService ContentStreamProcessorService;
    
    private AccountDatabase _accountDatabase;
    private UserDatabase _userDatabase;
    private SessionDatabase _sessionDatabase;
    
    private CosmosClient _cosmosClient;

    private readonly string _container = "test";
    private readonly string _databaseId = "socialapp";
    
    private readonly CancellationTokenSource _changeFeedCancellationToken = new CancellationTokenSource();
    
    public Exception FeedError { get; private set; }
    
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

        AccountService = new AccountService(_accountDatabase, _userDatabase);
        SessionService = new SessionService(_userDatabase, _sessionDatabase, _accountDatabase);
        FollowersService = new FollowersService(_userDatabase);
        ContentService = new ContentService(_userDatabase);
        FeedService = new FeedService(_userDatabase);
        ContentStreamProcessorService = new ContentStreamProcessorService(_userDatabase, new Logger<ContentStreamProcessorService>(new LoggerFactory()));
        
        await _userDatabase.InitializeAsync();
        
        _ = Task.Run(ProcessChangeFeedAsync);
    }

    private async Task ProcessChangeFeedAsync()
    {
        try
        {
            await ContentStreamProcessorService.ProcessChangeFeedAsync(_changeFeedCancellationToken.Token);
        }
        catch (Exception e)
        {
            FeedError = e;
        }
    }

    [OneTimeTearDown]
    public async Task TearDown()
    {
        var feedError = FeedError;
        await _changeFeedCancellationToken.CancelAsync();
        _changeFeedCancellationToken.Dispose();
        if(RemoveContainerAfterTests)
            await _cosmosClient.GetDatabase(_databaseId).GetContainer(_container).DeleteContainerAsync();
        _cosmosClient.Dispose();
        if(feedError != null)
            throw feedError;
    }
    
    protected async Task<UserSession> CreateUserAsync(string username = "")
    {
        var userName = username + Guid.NewGuid().ToString("N");
        await AccountService.RegisterAsync(new ($"{userName}@xxx.com", userName, "Display"+userName, "pass"), OperationContext.New());
        var session = await SessionService.LoginWithPasswordAsync(new($"{userName}@xxx.com", "pass"), OperationContext.New());
        return session ?? throw new InvalidOperationException("Cannot find user");
    }
    
    protected static CosmosException CreateCosmoException(HttpStatusCode code = HttpStatusCode.InternalServerError)
        => new(code.ToString(), code, (int)code, Guid.NewGuid().ToString(), 2);
}