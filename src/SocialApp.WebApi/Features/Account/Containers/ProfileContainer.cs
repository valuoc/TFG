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
    

    public async Task<string?> FindPasswordLoginAsync(string email, string password, OperationContext context)
    {
        var loginKey = PasswordLoginDocument.Key(email);
        var response = await Container.ReadItemAsync<PasswordLoginDocument>(loginKey.Id, new PartitionKey(loginKey.Pk), cancellationToken: context.Cancellation);
        context.AddRequestCharge(response.RequestCharge);
        if (response.Resource == null || response.Resource.Password != Passwords.HashPassword(password))
        {
            return null;
        }
        return response.Resource.UserId;
    }
}