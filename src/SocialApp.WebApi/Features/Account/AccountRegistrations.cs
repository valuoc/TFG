using Microsoft.Extensions.Caching.Memory;
using SocialApp.WebApi.Data._Shared;
using SocialApp.WebApi.Data.Account;
using SocialApp.WebApi.Data.User;
using SocialApp.WebApi.Features._Shared.Services;
using SocialApp.WebApi.Features.Account.Queries;
using SocialApp.WebApi.Features.Account.Services;
using SocialApp.WebApi.Infrastructure.Jobs;

namespace SocialApp.WebApi.Features.Account;

public static class AccountRegistrations
{
    public static void RegisterAccountServices(this IServiceCollection services)
    {
        services.AddSingleton<IAccountService, AccountService>();
        services.AddHostedService<PendingAccountCleanJob>();
        services.AddSingleton<IQueryManyHandler<ExpiredPendingAccountsQueryMany, PendingAccountDocument>, ExpiredPendingAccountsCosmosDbQueryManyHandler>();
        services.AddSingleton<IQuerySingleHandler<ProfileQuery, ProfileDocument?>, ProfileQueryHandler>();
        services.AddSingleton<IQueryManyHandler<ProfilesQuery, (DocumentKey, ProfileDocument?)>, ProfilesQueryHandler>();
        services.AddSingleton<IUserHandleService>(s => new UserHandleServiceCacheDecorator
        (
            new UserHandleService(s.GetRequiredService<UserDatabase>(), s.GetRequiredService<IQueries>()), s.GetRequiredService<IMemoryCache>()
        ));
    }
}