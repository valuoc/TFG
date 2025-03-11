using Microsoft.Azure.Cosmos;
using SocialApp.WebApi.Features.Databases;

namespace SocialApp.WebApi.Features.Session.Databases;

public sealed class SessionDatabase : CosmoDatabase
{
    public SessionDatabase(CosmosClient cosmosClient, string databaseId, string containerId)
        :base(cosmosClient, databaseId, containerId) { }
    public Container GetSessionContainer()
    {
        return CosmosClient.GetContainer(DatabaseId, ContainerId);
    }
}