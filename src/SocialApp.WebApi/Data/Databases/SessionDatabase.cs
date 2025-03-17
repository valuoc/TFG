using Microsoft.Azure.Cosmos;
using SocialApp.WebApi.Features.Databases;

namespace SocialApp.WebApi.Features.Account.Databases;

/// <summary>
/// Likely to be just a cache
/// </summary>
public sealed class SessionDatabase : CosmoDatabase
{
    public SessionDatabase(CosmosClient cosmosClient, string databaseId, string containerId)
        :base(cosmosClient, databaseId, containerId) { }
}