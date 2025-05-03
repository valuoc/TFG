using SocialApp.WebApi.Data._Shared;
using SocialApp.WebApi.Data.User;

namespace SocialApp.WebApi.Features.Follow.Containers;

public sealed class FollowContainer : CosmoContainer
{
    public FollowContainer(UserDatabase userDatabase)
        :base(userDatabase, "follows") { }
}