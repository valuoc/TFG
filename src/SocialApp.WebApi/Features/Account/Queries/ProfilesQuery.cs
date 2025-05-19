using SocialApp.WebApi.Data._Shared;
using SocialApp.WebApi.Data.User;
using SocialApp.WebApi.Features._Shared.Services;

namespace SocialApp.WebApi.Features.Account.Queries;

public class ProfilesQuery : IQueryMany<(DocumentKey, ProfileDocument?)>
{
    public DocumentKey[] ProfileKeys { get; set; }
}

public sealed class ProfilesQueryHandler : IQueryManyHandler<ProfilesQuery, (DocumentKey, ProfileDocument?)>
{
    public async IAsyncEnumerable<(DocumentKey, ProfileDocument?)> ExecuteQueryAsync(CosmoContainer container, ProfilesQuery query, OperationContext context)
    {
        foreach (var item in await container.GetManyAsync<ProfileDocument?>(query.ProfileKeys, context))
            yield return (item.Key, item.Value);
    }
}