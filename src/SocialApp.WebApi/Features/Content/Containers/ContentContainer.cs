using SocialApp.WebApi.Data._Shared;
using SocialApp.WebApi.Data.User;

namespace SocialApp.WebApi.Features.Content.Containers;

public sealed class ContentContainer : CosmoContainer
{
    public ContentContainer(UserDatabase database)
        :base(database, "contents") { }
}