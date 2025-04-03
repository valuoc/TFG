using SocialApp.WebApi.Features._Shared.Services;
using SocialApp.WebApi.Features.Follow.Exceptions;

namespace SocialApp.Tests.ServicesTests;

[Order(3)]
public class FollowServiceTests: ServiceTestsBase
{
    [Test, Order(1)]
    public async Task Follows_Can_Be_Added()
    {
        var user1 = await CreateUserAsync();
        var user2 = await CreateUserAsync();

        for (var i = 0; i < 2; i++)
        {
            await FollowersService.AddAsync(user1.UserId, user2.UserId, OperationContext.New());

            var followers1 = await FollowersService.GetFollowersAsync(user1.UserId, OperationContext.New());
            Assert.That(followers1, Is.Empty);

            var followings1 = await FollowersService.GetFollowingsAsync(user1.UserId, OperationContext.New());
            Assert.That(followings1, Is.Not.Empty);
            Assert.That(followings1.Count, Is.EqualTo(1));
            Assert.That(followings1, Contains.Item(user2.UserId));
        
            var followers2 = await FollowersService.GetFollowersAsync(user2.UserId, OperationContext.New());
            Assert.That(followers2, Is.Not.Empty);
            Assert.That(followers2.Count, Is.EqualTo(1));
            Assert.That(followers2, Contains.Item(user1.UserId));
        
            var followings2 = await FollowersService.GetFollowingsAsync(user2.UserId, OperationContext.New());
            Assert.That(followings2, Is.Empty);
        }
        
        await FollowersService.RemoveAsync(user1.UserId, user2.UserId, OperationContext.New());
    }
    
    [Test, Order(2)]
    public async Task Follows_Can_Be_Removed()
    {
        var user1 = await CreateUserAsync();
        var user2 = await CreateUserAsync();

        await FollowersService.AddAsync(user1.UserId, user2.UserId, OperationContext.New());

        for (var i = 0; i < 2; i++)
        {
            await FollowersService.RemoveAsync(user1.UserId, user2.UserId, OperationContext.New());

            var followers1 = await FollowersService.GetFollowersAsync(user1.UserId, OperationContext.New());
            Assert.That(followers1, Is.Empty);

            var followings1 = await FollowersService.GetFollowingsAsync(user1.UserId, OperationContext.New());
            Assert.That(followings1, Is.Empty);

            var followers2 = await FollowersService.GetFollowersAsync(user2.UserId, OperationContext.New());
            Assert.That(followers2, Is.Empty);

            var followings2 = await FollowersService.GetFollowingsAsync(user2.UserId, OperationContext.New());
            Assert.That(followings2, Is.Empty);
        }
    }

    [Test, Order(3)]
    public async Task Follows_CanRecoverFromFailure_BeforeSavingFollower()
    {
        var user1 = await CreateUserAsync();
        var user2 = await CreateUserAsync();
        var user3 = await CreateUserAsync();

        var context = OperationContext.New();
        context.FailOnSignal("add-follower", CreateCosmoException());
        Assert.ThrowsAsync<FollowerException>(()=> FollowersService.AddAsync(user1.UserId, user2.UserId, context));

        var followers1 = await FollowersService.GetFollowersAsync(user1.UserId, OperationContext.New());
        Assert.That(followers1, Is.Empty);
        var followings1 = await FollowersService.GetFollowingsAsync(user1.UserId, OperationContext.New());
        Assert.That(followings1, Is.Empty);
        var followers2 = await FollowersService.GetFollowersAsync(user2.UserId, OperationContext.New());
        Assert.That(followers2, Is.Empty);

        context = OperationContext.New();
        await FollowersService.AddAsync(user1.UserId, user3.UserId, context);
        followings1 = await FollowersService.GetFollowingsAsync(user1.UserId, OperationContext.New());
        Assert.That(followings1.Count, Is.EqualTo(1));
    }
    
    [Test, Order(4)]
    public async Task Follows_CanRecoverFromFailure_AfterSavingFollower()
    {
        var user1 = await CreateUserAsync();
        var user2 = await CreateUserAsync();
        var user3 = await CreateUserAsync();

        var context = OperationContext.New();
        context.FailOnSignal("add-following", CreateCosmoException());
        Assert.ThrowsAsync<FollowerException>(()=> FollowersService.AddAsync(user1.UserId, user2.UserId, context));

        var followers1 = await FollowersService.GetFollowersAsync(user1.UserId, OperationContext.New());
        Assert.That(followers1, Is.Empty);
        var followings1 = await FollowersService.GetFollowingsAsync(user1.UserId, OperationContext.New());
        Assert.That(followings1, Is.Empty);
        var followers2 = await FollowersService.GetFollowersAsync(user2.UserId, OperationContext.New());
        Assert.That(followers2.Count, Is.EqualTo(1));

        context = OperationContext.New();
        await FollowersService.AddAsync(user1.UserId, user3.UserId, context);
        followings1 = await FollowersService.GetFollowingsAsync(user1.UserId, OperationContext.New());
        Assert.That(followings1.Count, Is.EqualTo(2));
    }
}