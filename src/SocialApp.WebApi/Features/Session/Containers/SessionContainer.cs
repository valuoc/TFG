using SocialApp.WebApi.Data._Shared;
using SocialApp.WebApi.Data.Session;

namespace SocialApp.WebApi.Features.Session.Containers;

public sealed class SessionContainer : CosmoContainer
{
    public SessionContainer(SessionDatabase database)
        :base(database, "sessions")
    { }
}