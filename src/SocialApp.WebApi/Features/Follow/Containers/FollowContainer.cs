using Microsoft.Azure.Cosmos;
using SocialApp.WebApi.Data._Shared;
using SocialApp.WebApi.Data.User;
using SocialApp.WebApi.Features._Shared.Services;

namespace SocialApp.WebApi.Features.Follow.Containers;

public sealed class FollowContainer : CosmoContainer
{
    public FollowContainer(UserDatabase userDatabase)
        :base(userDatabase, "follows") { }
    
    public async Task SaveFollowingsAsync(FollowingListDocument followings, OperationContext context)
    {
        var response = await Container.ReplaceItemAsync(followings, followings.Id, requestOptions: new ItemRequestOptions
        {
            EnableContentResponseOnWrite = false,
            IfMatchEtag = followings?.ETag
        }, cancellationToken: context.Cancellation);
        context.AddRequestCharge(response.RequestCharge);
    }
    
    public async Task<FollowingListDocument> CreateOrReplaceFollowingsAsync(FollowingListDocument followings, OperationContext context)
    {
        var response = await Container.UpsertItemAsync(followings, requestOptions: new ItemRequestOptions
        {
            EnableContentResponseOnWrite = true,
            IfMatchEtag = followings?.ETag
        }, cancellationToken: context.Cancellation);
        context.AddRequestCharge(response.RequestCharge);
        return response.Resource;
    }

    public async Task SaveFollowersAsync(FollowerListDocument followers, OperationContext context)
    {
        var response = await Container.UpsertItemAsync(followers, requestOptions: new ItemRequestOptions
        {
            EnableContentResponseOnWrite = false,
            IfMatchEtag = followers?.ETag
        }, cancellationToken: context.Cancellation);
        context.AddRequestCharge(response.RequestCharge);
    }
    
    public async Task<FollowerListDocument?> GetFollowersAsync(string followedId, OperationContext context)
    {
        try
        {
            var followerKey = FollowerListDocument.Key(followedId);
            var response = await Container.ReadItemAsync<FollowerListDocument>(followerKey.Id, new PartitionKey(followerKey.Pk), cancellationToken: context.Cancellation);
            context.AddRequestCharge(response.RequestCharge);
            return response.Resource;
        }
        catch (CosmosException e) when (e.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<FollowingListDocument?> GetFollowingAsync(string followerId, OperationContext context)
    {
        try
        {
            var followingKey = FollowingListDocument.Key(followerId);
            var response = await Container.ReadItemAsync<FollowingListDocument>(followingKey.Id, new PartitionKey(followingKey.Pk), cancellationToken: context.Cancellation);
            context.AddRequestCharge(response.RequestCharge);
            return response.Resource;
        }
        catch (CosmosException e) when (e.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }
    
}