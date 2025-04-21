using SocialApp.Models.Content;

namespace SocialApp.ClientApi.Services;

public sealed class ContentService
{
    private readonly SocialAppClient _client;

    public ContentService(SocialAppClient client)
        => _client = client;

    public async Task<string> StartConversationAsync(string content, CancellationToken cancel = default)
    {
        var response = await _client.PostAsync("/conversation", new ContentRequest { Content = content }, cancel);
        return response.Headers?.Location?.ToString().Substring(response.Headers.Location.ToString().LastIndexOf('/') + 1) ?? string.Empty;
    }

    public async Task<ConversationModel> GetConversationAsync(string handle, string conversationId, CancellationToken cancel = default)
        => (await _client.GetAsync<ConversationModel>($"/conversation/{handle}/{conversationId}", cancel)).Content;
    
    public async Task<IReadOnlyList<CommentModel>> GetConversationCommentsBeforeAsync(string handle, string conversationId, string before, CancellationToken cancel = default)
        => (await _client.GetAsync<IReadOnlyList<CommentModel>>($"/conversation/{handle}/{conversationId}/comments?before={before}", cancel)).Content;

    public async Task UpdateConversationAsync(string handle, string conversationId, string content, CancellationToken cancel = default)
        => await _client.PutAsync($"/conversation/{handle}/{conversationId}", new ContentRequest{ Content = content}, cancel);
    
    public async Task ReactToConversationAsync(string handle, string conversationId, bool like, CancellationToken cancel = default)
        => await _client.PutAsync($"/conversation/{handle}/{conversationId}/like", new ReactContent(){ Like = like}, cancel);
    
    public async Task DeleteConversationAsync(string handle, string conversationId, CancellationToken cancel = default)
        => await _client.DeleteAsync($"/conversation/{handle}/{conversationId}", cancel);

    public async Task<string> CommentAsync(string handle, string conversationId, string content, CancellationToken cancel = default)
    {
        var response = await _client.PostAsync($"/conversation/{handle}/{conversationId}", new ContentRequest { Content = content }, cancel);
        return response.Headers?.Location?.ToString().Substring(response.Headers.Location.ToString().LastIndexOf('/') + 1) ?? string.Empty;
    }

    public async Task<IReadOnlyList<ConversationHeaderModel>> GetConversationsAsync(string handle, string? before = null, CancellationToken cancel = default)
        => (await _client.GetAsync<IReadOnlyList<ConversationHeaderModel>>($"/conversation/{handle}?before={before}", cancel)).Content;
}