using Microsoft.Azure.Cosmos;
using SocialApp.WebApi.Data.Shared;

namespace SocialApp.WebApi.Data.Account;

/// <summary>
/// Cannot be geo-distributed, because it is used for holding the unique
/// identifiers associated with the account.
/// </summary>

public sealed class AccountDatabase : CosmoDatabase
{
    public AccountDatabase(CosmosClient cosmosClient, string databaseId, string containerId)
        :base(cosmosClient, databaseId, containerId) { }
}