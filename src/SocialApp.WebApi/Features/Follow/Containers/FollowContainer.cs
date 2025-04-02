using Microsoft.Azure.Cosmos;
using SocialApp.WebApi.Data._Shared;
using SocialApp.WebApi.Data.User;
using SocialApp.WebApi.Features._Shared.Services;

namespace SocialApp.WebApi.Features.Follow.Containers;

public sealed class FollowContainer : CosmoContainer
{
    public FollowContainer(UserDatabase userDatabase)
        :base(userDatabase) { }
    
    public async Task SaveFollowingsAsync(FollowingListDocument followings, OperationContext context)
    {
        await Container.ReplaceItemAsync(followings, followings.Id, requestOptions: new ItemRequestOptions
        {
            EnableContentResponseOnWrite = false,
            IfMatchEtag = followings?.ETag
        }, cancellationToken: context.Cancellation);
    }
    
    public async Task<FollowingListDocument> CreateOrReplaceFollowingsAsync(FollowingListDocument followings, OperationContext context)
    {
        var response = await Container.UpsertItemAsync(followings, requestOptions: new ItemRequestOptions
        {
            EnableContentResponseOnWrite = true,
            IfMatchEtag = followings?.ETag
        }, cancellationToken: context.Cancellation);
        return response.Resource;
    }

    public async Task SaveFollowersAsync(FollowerListDocument followers, OperationContext context)
    {
        await Container.UpsertItemAsync(followers, requestOptions: new ItemRequestOptions
        {
            EnableContentResponseOnWrite = false,
            IfMatchEtag = followers?.ETag
        }, cancellationToken: context.Cancellation);
    }
    
    public async Task<FollowerListDocument?> GetFollowersAsync(string followedId, CancellationToken cancel)
    {
        try
        {
            var followerKey = FollowerListDocument.Key(followedId);
            var followerResponse = await Container.ReadItemAsync<FollowerListDocument>(followerKey.Id, new PartitionKey(followerKey.Pk), cancellationToken: cancel);
            return followerResponse.Resource;
        }
        catch (CosmosException e) when (e.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<FollowingListDocument?> GetFollowingAsync(string followerId, CancellationToken cancel)
    {
        try
        {
            var followingKey = FollowingListDocument.Key(followerId);
            var followingResponse = await Container.ReadItemAsync<FollowingListDocument>(followingKey.Id, new PartitionKey(followingKey.Pk), cancellationToken: cancel);
            return followingResponse.Resource;
        }
        catch (CosmosException e) when (e.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }
    
}