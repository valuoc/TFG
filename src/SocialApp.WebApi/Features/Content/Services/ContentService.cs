using SocialApp.WebApi.Features.Content.Databases;

namespace SocialApp.WebApi.Features.Content.Services;

public sealed class ContentService
{
    private readonly ContentDatabase _contentDb;

    public ContentService(ContentDatabase contentDb)
    {
        _contentDb = contentDb;
    }
}