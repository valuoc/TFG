using System.Security.Claims;
using SocialApp.WebApi.Data.Account;
using SocialApp.WebApi.Data.Session;
using SocialApp.WebApi.Data.Shared;
using SocialApp.WebApi.Data.User;
using SocialApp.WebApi.Features._Shared.Services;
using SocialApp.WebApi.Features.Account.Services;
using SocialApp.WebApi.Features.Follow.Services;
using SocialApp.WebApi.Features.Session.Models;
using SocialApp.WebApi.Features.Session.Services;
using SocialApp.WebApi.Infrastructure;

namespace SocialApp.WebApi;

public static class RegisterDependencies
{
    public static void RegisterServices(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<OperationContext>(s =>
        {
            var http = s.GetRequiredService<IHttpContextAccessor>().HttpContext;
            return new OperationContext(http.RequestAborted);
        });
        services.AddScoped<SessionGetter>();
        
        services.AddSingleton(GetAccountDatabase);
        services.AddSingleton(GetUserDatabase);
        services.AddSingleton(GetSessionDatabase);
        
        services.AddSingleton<IAccountService, AccountService>();
        services.AddSingleton<ISessionService, SessionService>();
        services.AddSingleton<IFollowersService, FollowersService>();
    }

    private static SessionDatabase GetSessionDatabase(IServiceProvider services)
    {
        var config = services.GetRequiredService<IConfiguration>();
            
        var endpoint = config.GetValue<string>("CosmosDb:Endpoint", null) ?? throw new InvalidOperationException("Missing CosmosDb Endpoint");
        var authKey = config.GetValue<string>("CosmosDb:AuthKey", null) ?? throw new InvalidOperationException("Missing CosmosDb AuthKey");
        var applicationName = config.GetValue<string>("CosmosDb:ApplicationName", null) ?? throw new InvalidOperationException("Missing CosmosDb ApplicationName");
        
        var cosmosClient = CosmoDatabase.CreateCosmosClient
        (
            endpoint, 
            authKey, 
            applicationName
        );
            
        return new SessionDatabase(cosmosClient, "socialapp", "test");
    }

    private static UserDatabase GetUserDatabase(IServiceProvider services)
    {
        var config = services.GetRequiredService<IConfiguration>();
            
        var endpoint = config.GetValue<string>("CosmosDb:Endpoint", null) ?? throw new InvalidOperationException("Missing CosmosDb Endpoint");
        var authKey = config.GetValue<string>("CosmosDb:AuthKey", null) ?? throw new InvalidOperationException("Missing CosmosDb AuthKey");
        var applicationName = config.GetValue<string>("CosmosDb:ApplicationName", null) ?? throw new InvalidOperationException("Missing CosmosDb ApplicationName");
        
        var cosmosClient = CosmoDatabase.CreateCosmosClient
        (
            endpoint, 
            authKey, 
            applicationName
        );
            
        return new UserDatabase(cosmosClient, "socialapp", "test");
    }

    private static AccountDatabase GetAccountDatabase(IServiceProvider services)
    {
        var config = services.GetRequiredService<IConfiguration>();
            
        var endpoint = config.GetValue<string>("CosmosDb:Endpoint", null) ?? throw new InvalidOperationException("Missing CosmosDb Endpoint");
        var authKey = config.GetValue<string>("CosmosDb:AuthKey", null) ?? throw new InvalidOperationException("Missing CosmosDb AuthKey");
        var applicationName = config.GetValue<string>("CosmosDb:ApplicationName", null) ?? throw new InvalidOperationException("Missing CosmosDb ApplicationName");
        
        var cosmosClient = CosmoDatabase.CreateCosmosClient
        (
            endpoint, 
            authKey, 
            applicationName
        );
            
        return new AccountDatabase(cosmosClient, "socialapp", "test");
    }
}