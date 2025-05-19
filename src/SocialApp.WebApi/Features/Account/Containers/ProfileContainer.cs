using SocialApp.WebApi.Data._Shared;
using SocialApp.WebApi.Data.User;

namespace SocialApp.WebApi.Features.Account.Containers;

public sealed class ProfileContainer : CosmoContainer
{
    public ProfileContainer(UserDatabase database)
        :base(database, "profiles")
    { }
}