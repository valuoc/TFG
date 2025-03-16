using Microsoft.Azure.Cosmos;
using SocialApp.WebApi.Features.Databases;

namespace SocialApp.WebApi.Features.Account.Databases;

public sealed class UserDatabase : CosmoDatabase
{
    public UserDatabase(CosmosClient cosmosClient, string databaseId, string containerId)
        :base(cosmosClient, databaseId, containerId) { }
}