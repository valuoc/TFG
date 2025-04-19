using SocialApp.Models.Account;
using SocialApp.Models.Follows;

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
        return await _client.GetAsync<IReadOnlyList<string>>("/followings", cancel);
    }
    
    public async Task FollowAsync(string otherUserId, CancellationToken cancel = default)
    {
        await _client.PostAsync("/followings", new OtherUserRequest(){UserId = otherUserId}, cancel);
    }
    
    public async Task UnfollowAsync(string otherUserId, CancellationToken cancel = default)
    {
        await _client.DeleteAsync($"/followings/{otherUserId}", cancel);
    }
}