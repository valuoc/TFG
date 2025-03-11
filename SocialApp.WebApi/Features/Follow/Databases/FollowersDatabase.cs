using Microsoft.Azure.Cosmos;
using SocialApp.WebApi.Features.Databases;

namespace SocialApp.WebApi.Features.Follow.Databases;

public sealed class FollowersDatabase : CosmoDatabase
{
    public FollowersDatabase(CosmosClient cosmosClient, string databaseId, string containerId)
        : base(cosmosClient, databaseId, containerId) { }
}