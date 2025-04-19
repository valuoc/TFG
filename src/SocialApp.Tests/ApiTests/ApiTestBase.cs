using SocialApp.ClientApi;

namespace SocialApp.Tests.ApiTests;

public abstract class ApiTestBase
{
    protected static SocialAppClient CreateClient()
    {
        return new SocialAppClient(new Uri("http://localhost:5081"));
    }
}