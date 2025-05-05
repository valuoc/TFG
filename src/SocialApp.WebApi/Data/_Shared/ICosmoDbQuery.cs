using Microsoft.Azure.Cosmos;

namespace SocialApp.WebApi.Data._Shared;

public interface ICosmoDbQuery<in T>
{
    public QueryDefinition GetQuery(T query);
}