using SocialApp.Models.Session;

namespace SocialApp.ClientApi.Services;

public sealed class SessionService
{
    private readonly SocialAppClient _client;

    public SessionService(SocialAppClient client)
    {
        _client = client;
    }

    public async Task LoginAsync(LoginRequest request, CancellationToken cancel = default)
    {
        await _client.PostAsync("/login", request, cancel);
    }
    
    public async Task LogoutAsync(CancellationToken cancel = default)
    {
        await _client.PostAsync("/logout", cancel);
    }
}