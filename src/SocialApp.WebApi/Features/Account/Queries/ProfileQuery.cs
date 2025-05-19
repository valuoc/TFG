using Microsoft.Azure.Cosmos;
using SocialApp.WebApi.Data._Shared;
using SocialApp.WebApi.Data.User;
using SocialApp.WebApi.Features._Shared.Services;

namespace SocialApp.WebApi.Features.Account.Queries;

public class ProfileQuery : IQuerySingle<ProfileDocument?>
{
    public string UserId { get; set; }
}

public sealed class ProfileQueryHandler : IQuerySingleHandler<ProfileQuery, ProfileDocument?>
{
    public async Task<ProfileDocument?> ExecuteQueryAsync(CosmoContainer container, ProfileQuery query, OperationContext context)
    {
        var profileKey = ProfileDocument.Key(query.UserId);
        const string sql = "select * from c where c.pk = @pk and c.type = @a";
        var cosmosDb = new QueryDefinition(sql)
            .WithParameter("@pk", profileKey.Pk)
            .WithParameter("@a", nameof(ProfileDocument));

        ProfileDocument? profile = null;
        await foreach (var document in container.ExecuteQueryReaderAsync(cosmosDb, profileKey.Pk, context))
        {
            if(document is ProfileDocument p)
                profile = p;
            else
                throw new InvalidOperationException("Unexpected document type: " + document.GetType().Name);
        }

        return profile;
    }
}