using SocialApp.ClientApi;

namespace SocialApp.Tests.ApiTests;

public abstract class ApiTestBase
{
    protected static SocialAppClient CreateClient(string url)
    {
        return new SocialAppClient(new Uri(url));
    }
}