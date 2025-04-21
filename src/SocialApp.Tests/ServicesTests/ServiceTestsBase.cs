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
    protected bool RemoveContainerAfterTests = true;
    
    private readonly IConfiguration _configuration;
    
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
    
    private readonly CancellationTokenSource _changeFeedCancellationToken = new CancellationTokenSource();
    
    public Exception FeedError { get; private set; }

    protected ServiceTestsBase()
    {
        _configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.Local.json", optional: true)
            .Build();
    }
    
    [OneTimeSetUp]
    public async Task Setup()
    {
        _cosmosClient = CosmoDatabase.CreateCosmosClient
        (
            _configuration.GetSection("CosmosDb:User"),
            _configuration.GetValue<string>("CosmosDb:ApplicationName") ?? throw new SocialAppConfigurationException("Missing CosmosDb ApplicationName")
        );
        
        _accountDatabase = new AccountDatabase(_cosmosClient, _configuration.GetSection("CosmosDb:Account"));
        _userDatabase = new UserDatabase(_cosmosClient, _configuration.GetSection("CosmosDb:User"));
        _sessionDatabase = new SessionDatabase(_cosmosClient, _configuration.GetSection("CosmosDb:Session"));

        AccountService = new AccountService(_accountDatabase, _userDatabase);
        SessionService = new SessionService(_userDatabase, _sessionDatabase, _accountDatabase);
        FollowersService = new FollowersService(_userDatabase);
        var userHandleService = new UserHandleServiceCacheDecorator(new UserHandleService(_accountDatabase));
        ContentService = new ContentService(_userDatabase, userHandleService);
        FeedService = new FeedService(_userDatabase, userHandleService);
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
            await _cosmosClient.GetDatabase(_configuration.GetValue<string>("CosmosDb:User:Id")).GetContainer(_configuration.GetValue<string>("CosmosDb:User:Containers:Contents:Id")).DeleteContainerAsync();
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