using SocialApp.WebApi.Data.Account;
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
        services.AddSingleton<IQueryMany<ExpiredPendingAccountsQuery, PendingAccountDocument>, ExpiredPendingAccountsCosmosDbQueryMany>();

    }
}