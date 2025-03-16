using Microsoft.Azure.Cosmos;
using SocialApp.WebApi.Features.Databases;

namespace SocialApp.WebApi.Features.Content.Databases;

public sealed class ContentDatabase : CosmoDatabase
{
    public ContentDatabase(CosmosClient cosmosClient, string databaseId, string containerId)
        :base(cosmosClient, databaseId, containerId) { }

    protected override IEnumerable<IncludedPath> GetIndexedPaths()
    {
        // We need to filter by post
        yield return new IncludedPath()
        {
            Path = "/type/?",
        };
    }

    public ContentContainer GetContentContainer()
    {
        return new ContentContainer(GetContainer(), this);
    }
}