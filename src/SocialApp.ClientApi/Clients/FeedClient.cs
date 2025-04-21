using SocialApp.Models.Content;

namespace SocialApp.ClientApi.Clients;

public sealed class FeedClient
{
    private readonly SocialAppClient _client;

    public FeedClient(SocialAppClient client)
    {
        _client = client;
    }

    public async Task<IReadOnlyList<ConversationRoot>> FeedAsync(string? before = null, CancellationToken cancel = default)
    {
        return (await _client.GetAsync<IReadOnlyList<ConversationRoot>>($"/feed?before={before}", cancel)).Content;
    }
}