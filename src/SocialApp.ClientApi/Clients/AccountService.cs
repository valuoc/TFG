using SocialApp.Models.Account;

namespace SocialApp.ClientApi.Clients;

public sealed class AccountService
{
    private readonly SocialAppClient _client;

    public AccountService(SocialAppClient client)
    {
        _client = client;
    }

    public async Task RegisterAsync(RegisterRequest request, CancellationToken cancel = default)
    {
        await _client.PostAsync("/register", request, cancel);
    }
}