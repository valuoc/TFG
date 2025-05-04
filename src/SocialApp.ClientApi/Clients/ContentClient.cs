using SocialApp.Models.Content;

namespace SocialApp.ClientApi.Clients;

public sealed class ContentClient
{
    private readonly SocialAppClient _client;

    public ContentClient(SocialAppClient client)
        => _client = client;

    public async Task<string> StartConversationAsync(string content, CancellationToken cancel = default)
    {
        var response = await _client.PostAsync("/conversation", new ContentRequest { Content = content }, cancel);
        return response.Headers?.Location?.ToString().Substring(response.Headers.Location.ToString().LastIndexOf('/') + 1) ?? string.Empty;
    }

    public async Task<Conversation> GetConversationAsync(string handle, string conversationId, CancellationToken cancel = default)
        => (await _client.GetAsync<Conversation>($"/conversation/{handle}/{conversationId}", cancel)).Content;
    
    public async Task<IReadOnlyList<ConversationComment>> GetConversationCommentsBeforeAsync(string handle, string conversationId, string before, CancellationToken cancel = default)
        => (await _client.GetAsync<IReadOnlyList<ConversationComment>>($"/conversation/{handle}/{conversationId}/comments?before={before}", cancel)).Content;

    public async Task UpdateConversationAsync(string handle, string conversationId, string content, CancellationToken cancel = default)
        => await _client.PutAsync($"/conversation/{handle}/{conversationId}", new ContentRequest{ Content = content}, cancel);
    
    public async Task ReactToConversationAsync(string handle, string conversationId, bool like, CancellationToken cancel = default)
        => await _client.PutAsync($"/conversation/{handle}/{conversationId}/like", new ReactRequest { Like = like}, cancel);
    
    public async Task DeleteConversationAsync(string handle, string conversationId, CancellationToken cancel = default)
        => await _client.DeleteAsync($"/conversation/{handle}/{conversationId}", cancel);

    public async Task<string> CommentAsync(string handle, string conversationId, string content, CancellationToken cancel = default)
    {
        var response = await _client.PostAsync($"/conversation/{handle}/{conversationId}", new ContentRequest { Content = content }, cancel);
        return response.Headers?.Location?.ToString().Substring(response.Headers.Location.ToString().LastIndexOf('/') + 1) ?? string.Empty;
    }

    public async Task<IReadOnlyList<ConversationRoot>> GetConversationsAsync(string handle, string? before = null, CancellationToken cancel = default)
        => (await _client.GetAsync<IReadOnlyList<ConversationRoot>>($"/conversation/{handle}?before={before}", cancel)).Content;
}