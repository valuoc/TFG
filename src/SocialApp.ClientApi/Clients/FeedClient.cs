using SocialApp.Models.Content;

namespace SocialApp.ClientApi.Clients;

public sealed class FeedClient
{
    private readonly SocialAppClient _client;

    public FeedClient(SocialAppClient client)
    {
        _client = client;
    }

    public async Task<IReadOnlyList<ConversationHeaderModel>> FeedAsync(string? before = null, CancellationToken cancel = default)
    {
        return (await _client.GetAsync<IReadOnlyList<ConversationHeaderModel>>($"/feed?before={before}", cancel)).Content;
    }
}