using SocialApp.WebApi.Data.Account;
using SocialApp.WebApi.Data.Session;
using SocialApp.WebApi.Data.Shared;
using SocialApp.WebApi.Data.User;
using SocialApp.WebApi.Features.Account.Services;

namespace SocialApp.WebApi;

public static class RegisterDependencies
{
    public static void RegisterServices(this IServiceCollection services)
    {
        services.AddSingleton<AccountDatabase>(s =>
        {
            var config = s.GetRequiredService<IConfiguration>();
            
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
        });
        services.AddSingleton<UserDatabase>(s =>
        {
            var config = s.GetRequiredService<IConfiguration>();
            
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
        });;
        services.AddSingleton<SessionDatabase>(s =>
        {
            var config = s.GetRequiredService<IConfiguration>();
            
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
        });;

        services.AddSingleton<IAccountService, AccountService>();
    }
}