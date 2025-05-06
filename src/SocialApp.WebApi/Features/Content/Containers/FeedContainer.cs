using SocialApp.WebApi.Data._Shared;
using SocialApp.WebApi.Data.User;

namespace SocialApp.WebApi.Features.Content.Containers;

public sealed class FeedContainer : CosmoContainer
{
    public FeedContainer(UserDatabase database)
        : base(database, "feeds")
    { }
}