using SocialApp.WebApi.Data._Shared;
using SocialApp.WebApi.Data.Account;
using SocialApp.WebApi.Data.Session;
using SocialApp.WebApi.Data.User;
using SocialApp.WebApi.Features._Shared.Services;
using SocialApp.WebApi.Features.Account;
using SocialApp.WebApi.Features.Content;
using SocialApp.WebApi.Features.Follow.Services;
using SocialApp.WebApi.Features.Session.Services;
using SocialApp.WebApi.Infrastructure;

namespace SocialApp.WebApi;

public static class RegisterDependencies
{
    public static void RegisterServices(this IServiceCollection services)
    {
        services.AddMemoryCache();
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
        
        services.AddSingleton<ISessionService, SessionService>();
        services.AddSingleton<IFollowersService, FollowersService>();
        
        services.AddSingleton<IQueries, QueryResolver>();
        services.RegisterAccountServices();
        services.RegisterContentServices();
    }

    private static SessionDatabase GetSessionDatabase(IServiceProvider services)
    {
        var configuration = services.GetRequiredService<IConfiguration>();
        
        var cosmosClient = CosmoDatabase.CreateCosmosClient
        (
            configuration.GetSection("CosmosDb:Session"),
            configuration.GetValue<string>("CosmosDb:ApplicationName") ?? throw new SocialAppConfigurationException("Missing CosmosDb ApplicationName")
        );
            
        return new SessionDatabase(cosmosClient, configuration.GetSection("CosmosDb:Session"));
    }

    private static UserDatabase GetUserDatabase(IServiceProvider services)
    {
        var configuration = services.GetRequiredService<IConfiguration>();
        
        var cosmosClient = CosmoDatabase.CreateCosmosClient
        (
            configuration.GetSection("CosmosDb:User"),
            configuration.GetValue<string>("CosmosDb:ApplicationName") ?? throw new SocialAppConfigurationException("Missing CosmosDb ApplicationName")
        );
            
        return new UserDatabase(cosmosClient, configuration.GetSection("CosmosDb:User"));
    }

    private static AccountDatabase GetAccountDatabase(IServiceProvider services)
    {
        var configuration = services.GetRequiredService<IConfiguration>();
        
        var cosmosClient = CosmoDatabase.CreateCosmosClient
        (
            configuration.GetSection("CosmosDb:Account"),
            configuration.GetValue<string>("CosmosDb:ApplicationName") ?? throw new SocialAppConfigurationException("Missing CosmosDb ApplicationName")
        );
            
        return new AccountDatabase(cosmosClient, configuration.GetSection("CosmosDb:Account"));
    }
}