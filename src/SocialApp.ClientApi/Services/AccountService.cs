using SocialApp.Models.Account;
using SocialApp.Models.Content;

namespace SocialApp.ClientApi.Services;

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

public sealed class FeedService
{
    private readonly SocialAppClient _client;

    public FeedService(SocialAppClient client)
    {
        _client = client;
    }

    public async Task<IReadOnlyList<ConversationHeaderModel>> FeedAsync(string? before = null, CancellationToken cancel = default)
    {
        return (await _client.GetAsync<IReadOnlyList<ConversationHeaderModel>>($"/feed?before={before}", cancel)).Content;
    }
}