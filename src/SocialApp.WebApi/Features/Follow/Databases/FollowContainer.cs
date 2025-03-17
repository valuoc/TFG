using Microsoft.Azure.Cosmos;
using SocialApp.WebApi.Features.Account.Databases;
using SocialApp.WebApi.Features.Follow.Documents;
using SocialApp.WebApi.Features.Services;

namespace SocialApp.WebApi.Features.Follow.Databases;

public sealed class FollowContainer
{
    private readonly Container _container;
    
    public FollowContainer(UserDatabase userDatabase)
    {
        _container = userDatabase.GetContainer();
    }
    
    public async Task SaveFollowingsAsync(FollowingListDocument followings, OperationContext context)
    {
        await _container.ReplaceItemAsync(followings, followings.Id, requestOptions: new ItemRequestOptions
        {
            EnableContentResponseOnWrite = false,
            IfMatchEtag = followings?.ETag
        }, cancellationToken: context.Cancellation);
    }
    
    public async Task<FollowingListDocument> CreateOrReplaceFollowingsAsync(FollowingListDocument followings, OperationContext context)
    {
        var response = await _container.UpsertItemAsync(followings, requestOptions: new ItemRequestOptions
        {
            EnableContentResponseOnWrite = true,
            IfMatchEtag = followings?.ETag
        }, cancellationToken: context.Cancellation);
        if(response.Resource != null)
            response.Resource.ETag = response.ETag;
        return response.Resource;
    }

    public async Task SaveFollowersAsync(FollowerListDocument followers, OperationContext context)
    {
        await _container.UpsertItemAsync(followers, requestOptions: new ItemRequestOptions
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
            var followerResponse = await _container.ReadItemAsync<FollowerListDocument>(followerKey.Id, new PartitionKey(followerKey.Pk), cancellationToken: cancel);
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
            var followingResponse = await _container.ReadItemAsync<FollowingListDocument>(followingKey.Id, new PartitionKey(followingKey.Pk), cancellationToken: cancel);
            if(followingResponse.Resource != null)
                followingResponse.Resource.ETag = followingResponse.ETag;
            return followingResponse.Resource;
        }
        catch (CosmosException e) when (e.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }
    
}