using System.Text;
using System.Text.Json;
using SocialApp.Models.Account;

namespace SocialApp.ClientApi.Services;

public sealed class AccountService
{
    private readonly SocialAppClient _client;

    public AccountService(SocialAppClient client)
    {
        _client = client;
    }

    public async Task<RegisterResponse?> RegisterAsync(RegisterRequest request, CancellationToken cancel = default)
    {
        return await _client.PostAsync<RegisterResponse>("/register", request, cancel);
    }
}