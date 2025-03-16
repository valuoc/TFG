using Microsoft.Azure.Cosmos;
using SocialApp.WebApi.Features.Services;
using SocialApp.WebApi.Features.Follow.Databases;
using SocialApp.WebApi.Features.Follow.Documents;
using SocialApp.WebApi.Features.Follow.Exceptions;

namespace SocialApp.WebApi.Features.Follow.Services;

public sealed class FollowersService
{
    private readonly FollowersDatabase _database;
    private static readonly ItemRequestOptions _noResponseContent = new(){ EnableContentResponseOnWrite = false};
    
    public FollowersService(FollowersDatabase database)
        => _database = database;

    public async ValueTask<IReadOnlyList<string>> GetFollowingsAsync(string userId, OperationContext context)
    {
        try
        {
            var container = _database.GetContainer();
        
            var followingResponse = await GetFollowingResponseAsync(container, userId, context.Cancellation);
            if (followingResponse?.Resource?.Following?.Count > 0)
            {
                var list = new List<string>(followingResponse.Resource.Following.Count);
                foreach (var following in followingResponse.Resource.Following)
                {
                    if(following.Value == FollowingStatus.Ready)
                        list.Add(following.Key);
                }
                return list;
            }

            return [];
        }
        catch (CosmosException e)
        {
            throw new FollowerException(FollowerError.UnexpectedError, e);
        }
    }
    
    public async ValueTask<IReadOnlyList<string>> GetFollowersAsync(string userId, OperationContext context)
    {
        try
        {
            var container = _database.GetContainer();
        
            var followerResponse = await GetFollowerResponseAsync(container, userId, context.Cancellation);
            return followerResponse?.Resource?.Followers?.ToList() ?? [];
        }
        catch (CosmosException e)
        {
            throw new FollowerException(FollowerError.UnexpectedError, e);
        }
    }
    
    public async ValueTask AddAsync(string followerId, string followedId, OperationContext context)
    {
        try
        {
            var container = _database.GetContainer();
        
            var followingResponse = await GetFollowingResponseAsync(container, followerId, context.Cancellation);
            var followings = followingResponse?.Resource ?? new FollowingListDocument(followerId);
            followings.Following ??= new();
            await ReconcilePendingAsync(followings, container, context);
        
            var exists = followings.Following.TryGetValue(followedId, out var status);
            if (exists && status == FollowingStatus.Ready)
                return;

            followings.Following[followedId] = FollowingStatus.PendingAdd;
        
            context.Signal("add-following-as-pending");
            followingResponse = await container.UpsertItemAsync(followings, requestOptions: new ItemRequestOptions
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
                context.Signal("add-follower");
                await container.UpsertItemAsync(followers, requestOptions: new ItemRequestOptions
                {
                    EnableContentResponseOnWrite = false,
                    IfMatchEtag = followerResponse?.ETag
                }, cancellationToken: context.Cancellation);
            }
            followings.Following[followedId] = FollowingStatus.Ready;
            context.Signal("add-following");
            await container.ReplaceItemAsync(followings, followings.Id, requestOptions: new ItemRequestOptions
            {
                EnableContentResponseOnWrite = false,
                IfMatchEtag = followingResponse?.ETag
            }, cancellationToken: context.Cancellation);
        }
        catch (CosmosException e) when (e.StatusCode == System.Net.HttpStatusCode.Conflict)
        {
            throw new FollowerException(FollowerError.ConcurrencyFailure, e);
        }
        catch (CosmosException e)
        {
            throw new FollowerException(FollowerError.UnexpectedError, e);
        }
    }

    public async ValueTask RemoveAsync(string followerId, string followedId, OperationContext context)
    {
        try
        {
            var container = _database.GetContainer();
        
            var followingResponse = await GetFollowingResponseAsync(container, followerId, context.Cancellation);
            var followings = followingResponse?.Resource ?? new FollowingListDocument(followerId);
            followings.Following ??= new();
        
            var exists = followings.Following.TryGetValue(followedId, out var status);
            if (!exists)
                return;

            await ReconcilePendingAsync(followings, container, context);
            followings.Following[followedId] = FollowingStatus.PendingRemove;
        
            context.Signal("remove-following-as-pending");
            followingResponse = await container.UpsertItemAsync(followings, requestOptions: new ItemRequestOptions
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
                context.Signal("remove-follower");
                await container.UpsertItemAsync(followers, requestOptions: new ItemRequestOptions
                {
                    EnableContentResponseOnWrite = false,
                    IfMatchEtag = followerResponse?.ETag
                }, cancellationToken: context.Cancellation);
            }

            followings.Following.Remove(followedId);
            context.Signal("remove-following");
            await container.ReplaceItemAsync(followings, followings.Id, requestOptions: new ItemRequestOptions
            {
                EnableContentResponseOnWrite = false,
                IfMatchEtag = followingResponse?.ETag
            }, cancellationToken: context.Cancellation);
        }
        catch (CosmosException e) when (e.StatusCode == System.Net.HttpStatusCode.Conflict)
        {
            throw new FollowerException(FollowerError.ConcurrencyFailure, e);
        }
        catch (CosmosException e)
        {
            throw new FollowerException(FollowerError.UnexpectedError, e);
        }
    }

    private async Task ReconcilePendingAsync(FollowingListDocument following, Container follows, OperationContext context)
    {
        foreach (var userId in following.Following.Keys)
        {
            if (following.Following[userId] != FollowingStatus.Ready)
            {
                // If it is pending adding or removing, check the other end and correct the status.
                var followerResponse = await GetFollowerResponseAsync(follows, userId, context.Cancellation);
                if (followerResponse?.Resource?.Followers?.Contains(following.UserId) ?? false)
                    following.Following[userId] = FollowingStatus.Ready;
                else
                    following.Following.Remove(userId);
            }
        }
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