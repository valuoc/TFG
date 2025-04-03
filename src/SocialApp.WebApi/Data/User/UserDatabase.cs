using System.Collections.ObjectModel;
using Microsoft.Azure.Cosmos;
using SocialApp.WebApi.Data._Shared;

namespace SocialApp.WebApi.Data.User;

public sealed class UserDatabase : CosmoDatabase
{
    public UserDatabase(CosmosClient cosmosClient, string databaseId, string containerId)
        :base(cosmosClient, databaseId, containerId) { }
    
    protected override IEnumerable<IncludedPath> GetIndexedPaths()
    {
        // We need to filter by post
        yield return new IncludedPath()
        {
            Path = "/sk/?"
        };
    }

    protected override IEnumerable<Collection<CompositePath>> GetCompositeIndexes()
    {
        yield return
        [
            new CompositePath()
            {
                Path = "/isRootThread",
                Order = CompositePathSortOrder.Ascending
            },
            new CompositePath()
            {
                Path = "/sk",
                Order = CompositePathSortOrder.Ascending
            }
        ];
    }
}