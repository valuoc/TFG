using Microsoft.Azure.Cosmos;
using SocialApp.WebApi.Data._Shared;

namespace SocialApp.WebApi.Data.Account;

/// <summary>
/// Cannot be geo-distributed, because it is used for holding the unique
/// identifiers associated with the account.
/// </summary>

public sealed class AccountDatabase : CosmoDatabase
{
    public AccountDatabase(CosmosClient cosmosClient, IConfiguration configuration)
        :base(cosmosClient, configuration) { }
}