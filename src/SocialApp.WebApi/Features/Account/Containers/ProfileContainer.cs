using Microsoft.Azure.Cosmos;
using SocialApp.WebApi.Data._Shared;
using SocialApp.WebApi.Data.User;
using SocialApp.WebApi.Features._Shared.Services;

namespace SocialApp.WebApi.Features.Account.Containers;

public sealed class ProfileContainer : CosmoContainer
{
    public ProfileContainer(UserDatabase database)
        :base(database, "profiles")
    { }
    
    public async Task<ProfileDocument?> GetProfileAsync(string userId, OperationContext context)
    {
        var profileKey = ProfileDocument.Key(userId);
        const string sql = "select * from c where c.pk = @pk and c.type = @a";
        var query = new QueryDefinition(sql)
            .WithParameter("@pk", profileKey.Pk)
            .WithParameter("@a", nameof(ProfileDocument));

        ProfileDocument? profile = null;
        await foreach (var document in ExecuteQueryReaderAsync(query, profileKey.Pk, context))
        {
            if(document is ProfileDocument p)
                profile = p;
            else
                throw new InvalidOperationException("Unexpected document type: " + document.GetType().Name);
        }

        return profile;
    }
}