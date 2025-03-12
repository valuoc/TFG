using Microsoft.Azure.Cosmos;
using SocialApp.WebApi.Features.Databases;

namespace SocialApp.WebApi.Features.Account.Databases;

public sealed class ProfileDatabase : CosmoDatabase
{
    public ProfileDatabase(CosmosClient cosmosClient, string databaseId, string containerId)
        :base(cosmosClient, databaseId, containerId) { }
}