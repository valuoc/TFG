namespace SocialApp.ClientApi.Services;

public sealed class FollowService
{
    private readonly SocialAppClient _client;

    public FollowService(SocialAppClient client)
    {
        _client = client;
    }

    public async Task<IReadOnlyList<string>> GetFollowersAsync(CancellationToken cancel = default)
    {
        return await _client.GetAsync<IReadOnlyList<string>>("/followers", cancel);
    }
    
    public async Task<IReadOnlyList<string>> GetFollowingsAsync(CancellationToken cancel = default)
    {
        return await _client.GetAsync<IReadOnlyList<string>>("/follow", cancel);
    }
    
    public async Task AddAsync(string otherUserId, CancellationToken cancel = default)
    {
        await _client.PostAsync($"/follow/{otherUserId}", cancel);
    }
    
    public async Task RemoveAsync(string otherUserId, CancellationToken cancel = default)
    {
        await _client.DeleteAsync($"/follow/{otherUserId}", cancel);
    }
}