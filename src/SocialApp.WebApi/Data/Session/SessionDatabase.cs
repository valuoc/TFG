using Microsoft.Azure.Cosmos;
using SocialApp.WebApi.Data._Shared;

namespace SocialApp.WebApi.Data.Session;

/// <summary>
/// Likely to be just a cache
/// </summary>
public sealed class SessionDatabase : CosmoDatabase
{
    public SessionDatabase(CosmosClient cosmosClient, IConfiguration configuration)
        :base(cosmosClient, configuration) { }
}