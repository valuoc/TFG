using Microsoft.Azure.Cosmos;
using SocialApp.WebApi.Data.User;
using SocialApp.WebApi.Features._Shared.Services;
using SocialApp.WebApi.Features.Follow.Containers;
using SocialApp.WebApi.Features.Follow.Exceptions;

namespace SocialApp.WebApi.Features.Follow.Services;

public sealed class FollowersService
{
    private readonly UserDatabase _database;
    private static readonly ItemRequestOptions _noResponseContent = new(){ EnableContentResponseOnWrite = false};
    
    public FollowersService(UserDatabase database)
        => _database = database;

    private FollowContainer GetContainer()
        => new(_database);

    public async ValueTask<IReadOnlyList<string>> GetFollowingsAsync(string userId, OperationContext context)
    {
        try
        {
            var container = GetContainer();
        
            var followingList = await container.GetFollowingAsync(userId, context);
            if (followingList?.Following?.Count > 0)
            {
                var list = new List<string>(followingList.Following.Count);
                foreach (var following in followingList.Following)
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
            var container = GetContainer();
        
            var followerList = await container.GetFollowersAsync(userId, context);
            return followerList?.Followers?.ToList() ?? [];
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
            var container = GetContainer();
        
            var followingList = await container.GetFollowingAsync(followerId, context);
            followingList ??= new FollowingListDocument(followerId);
            followingList.Following ??= new();
            await ReconcilePendingAsync(followingList, container, context);
        
            var exists = followingList.Following.TryGetValue(followedId, out var status);
            if (exists && status == FollowingStatus.Ready)
                return;

            followingList.Following[followedId] = FollowingStatus.PendingAdd;
        
            context.Signal("add-following-as-pending");
            followingList = await container.CreateOrReplaceFollowingsAsync(followingList, context);
        
            var followerList = await container.GetFollowersAsync(followedId, context);
            followerList ??= new FollowerListDocument(followedId);
            followerList.Followers ??= new HashSet<string>();

            if (followerList.Followers.Add(followerId))
            {
                context.Signal("add-follower");
                await container.SaveFollowersAsync(followerList, context);
            }
            followingList.Following[followedId] = FollowingStatus.Ready;
            context.Signal("add-following");
            await container.SaveFollowingsAsync(followingList, context);
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
            var container = GetContainer();
        
            var followingList = await container.GetFollowingAsync(followerId, context);
            followingList ??= new FollowingListDocument(followerId);
            followingList.Following ??= new();
        
            var exists = followingList.Following.TryGetValue(followedId, out var status);
            if (!exists)
                return;

            await ReconcilePendingAsync(followingList, container, context);
            followingList.Following[followedId] = FollowingStatus.PendingRemove;
        
            context.Signal("remove-following-as-pending");
            followingList = await container.CreateOrReplaceFollowingsAsync(followingList, context);
        
            var followerList = await container.GetFollowersAsync(followedId, context);
            followerList ??= new FollowerListDocument(followerId);
            followerList.Followers ??= new HashSet<string>();

            if (followerList.Followers.Remove(followerId))
            {
                context.Signal("remove-follower");
                await container.SaveFollowersAsync(followerList, context);
            }

            followingList.Following.Remove(followedId);
            context.Signal("remove-following");
            await container.SaveFollowingsAsync(followingList, context);
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
    
    private async Task ReconcilePendingAsync(FollowingListDocument following, FollowContainer container, OperationContext context)
    {
        foreach (var userId in following.Following.Keys)
        {
            if (following.Following[userId] != FollowingStatus.Ready)
            {
                // If it is pending adding or removing, check the other end and correct the status.
                var followerList = await container.GetFollowersAsync(userId, context);
                if (followerList?.Followers?.Contains(following.UserId) ?? false)
                    following.Following[userId] = FollowingStatus.Ready;
                else
                    following.Following.Remove(userId);
            }
        }
    }
}