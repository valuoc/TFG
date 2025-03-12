using Microsoft.Azure.Cosmos;
using SocialApp.WebApi.Features.Services;
using SocialApp.WebApi.Features.Follow.Databases;
using SocialApp.WebApi.Features.Follow.Documents;

namespace SocialApp.WebApi.Features.Follow.Services;

public sealed class FollowersService
{
    private readonly FollowersDatabase _database;
    private static readonly ItemRequestOptions _noResponseContent = new(){ EnableContentResponseOnWrite = false};
    
    public FollowersService(FollowersDatabase database)
        => _database = database;

    public async ValueTask<IReadOnlyList<string>> GetFollowingsAsync(string userId, OperationContext context)
    {
        var container = _database.GetContainer();
        
        var followingResponse = await GetFollowingResponseAsync(container, userId, context.Cancellation);
        return followingResponse?.Resource?.Following?.Where(x => x.Value == FollowingStatus.Ready).Select(x => x.Key).ToList() ?? [];
    }
    
    public async ValueTask<IReadOnlyList<string>> GetFollowersAsync(string userId, OperationContext context)
    {
        var container = _database.GetContainer();
        
        var followerResponse = await GetFollowerResponseAsync(container, userId, context.Cancellation);
        return followerResponse?.Resource?.Followers?.ToList() ?? [];
    }
    
    public async ValueTask AddAsync(string followerId, string followedId, OperationContext context)
    {
        var container = _database.GetContainer();
        
        var followingResponse = await GetFollowingResponseAsync(container, followerId, context.Cancellation);
        var followings = followingResponse?.Resource ?? new FollowingListDocument(followerId);
        followings.Following ??= new();
        
        var exists = followings.Following.TryGetValue(followedId, out var status);
        if (exists && status == FollowingStatus.Ready)
            return;

        followings.Following[followedId] = FollowingStatus.PendingAdd;
        
        followingResponse = await container.UpsertItemAsync(followings, requestOptions: new ItemRequestOptions()
        {
            EnableContentResponseOnWrite = true,
            IfMatchEtag = followingResponse?.ETag
        }, cancellationToken: context.Cancellation);
        followings = followingResponse.Resource;
        
        var followerResponse = await GetFollowerResponseAsync(container, followedId, context.Cancellation);
        var followers = followerResponse?.Resource ?? new FollowerListDocument(followedId);
        followers.Followers ??= new HashSet<string>();

        if (followers.Followers.Add(followerId))
        {
            await container.UpsertItemAsync(followers, requestOptions: new ItemRequestOptions()
            {
                EnableContentResponseOnWrite = false,
                IfMatchEtag = followerResponse?.ETag
            }, cancellationToken: context.Cancellation);
        }
        followings.Following[followedId] = FollowingStatus.Ready;
        await container.ReplaceItemAsync(followings, followings.Id, requestOptions: new ItemRequestOptions()
        {
            EnableContentResponseOnWrite = false,
            IfMatchEtag = followingResponse?.ETag
        }, cancellationToken: context.Cancellation);
    }

    public async ValueTask RemoveAsync(string followerId, string followedId, OperationContext context)
    {
        var container = _database.GetContainer();
        
        var followingResponse = await GetFollowingResponseAsync(container, followerId, context.Cancellation);
        var followings = followingResponse?.Resource ?? new FollowingListDocument(followerId);
        followings.Following ??= new();
        
        var exists = followings.Following.TryGetValue(followedId, out var status);
        if (!exists)
            return;

        followings.Following[followedId] = FollowingStatus.PendingRemove;
        
        followingResponse = await container.UpsertItemAsync(followings, requestOptions: new ItemRequestOptions()
        {
            EnableContentResponseOnWrite = true,
            IfMatchEtag = followingResponse?.ETag
        }, cancellationToken: context.Cancellation);
        followings = followingResponse.Resource;
        
        var followerResponse = await GetFollowerResponseAsync(container, followedId, context.Cancellation);
        var followers = followerResponse?.Resource ?? new FollowerListDocument(followerId);
        followers.Followers ??= new HashSet<string>();

        if (followers.Followers.Remove(followerId))
        {
            await container.UpsertItemAsync(followers, requestOptions: new ItemRequestOptions()
            {
                EnableContentResponseOnWrite = false,
                IfMatchEtag = followerResponse?.ETag
            }, cancellationToken: context.Cancellation);
        }

        followings.Following.Remove(followedId);
        await container.ReplaceItemAsync(followings, followings.Id, requestOptions: new ItemRequestOptions()
        {
            EnableContentResponseOnWrite = false,
            IfMatchEtag = followingResponse?.ETag
        }, cancellationToken: context.Cancellation);
    }
    
    private static async Task<ItemResponse<FollowerListDocument>?> GetFollowerResponseAsync(Container container, string followedId, CancellationToken cancel)
    {
        try
        {
            var followerKey = FollowerListDocument.Key(followedId);
            var followerResponse = await container.ReadItemAsync<FollowerListDocument>(followerKey.Id, new PartitionKey(followerKey.Pk), cancellationToken: cancel);
            return followerResponse;
        }
        catch (CosmosException e) when (e.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    private static async Task<ItemResponse<FollowingListDocument>?> GetFollowingResponseAsync(Container container, string followerId, CancellationToken cancel)
    {
        try
        {
            var followingKey = FollowingListDocument.Key(followerId);
            var followingResponse = await container.ReadItemAsync<FollowingListDocument>(followingKey.Id, new PartitionKey(followingKey.Pk), cancellationToken: cancel);
            return followingResponse;
        }
        catch (CosmosException e) when (e.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }
}