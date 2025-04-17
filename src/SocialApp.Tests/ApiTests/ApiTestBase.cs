using SocialApp.ClientApi;

namespace SocialApp.Tests.ApiTests;

public abstract class ApiTestBase
{
    protected readonly SocialAppClient Client;
    protected ApiTestBase()
    {
        Client = new SocialAppClient(new Uri("http://localhost:5081"));
    }
    
}