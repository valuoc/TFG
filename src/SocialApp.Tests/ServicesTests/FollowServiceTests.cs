using SocialApp.WebApi.Features.Services;
using SocialApp.WebApi.Features.Session.Services;

namespace SocialApp.Tests.ServicesTests;

[Order(2)]
public class FollowServiceTests: ServiceTestsBase
{
    [Test, Order(1)]
    public async Task Follows_Can_Be_Added()
    {
        var user1 = await CreateUserAsync();
        var user2 = await CreateUserAsync();

        for (var i = 0; i < 2; i++)
        {
            await FollowersService.AddAsync(user1.UserId, user2.UserId, OperationContext.None());

            var followers1 = await FollowersService.GetFollowersAsync(user1.UserId, OperationContext.None());
            Assert.That(followers1, Is.Empty);

            var followings1 = await FollowersService.GetFollowingsAsync(user1.UserId, OperationContext.None());
            Assert.That(followings1, Is.Not.Empty);
            Assert.That(followings1.Count, Is.EqualTo(1));
            Assert.That(followings1, Contains.Item(user2.UserId));
        
            var followers2 = await FollowersService.GetFollowersAsync(user2.UserId, OperationContext.None());
            Assert.That(followers2, Is.Not.Empty);
            Assert.That(followers2.Count, Is.EqualTo(1));
            Assert.That(followers2, Contains.Item(user1.UserId));
        
            var followings2 = await FollowersService.GetFollowingsAsync(user2.UserId, OperationContext.None());
            Assert.That(followings2, Is.Empty);
        }
        
        await FollowersService.RemoveAsync(user1.UserId, user2.UserId, OperationContext.None());
    }
    
    [Test, Order(2)]
    public async Task Follows_Can_Be_Removed()
    {
        var user1 = await CreateUserAsync();
        var user2 = await CreateUserAsync();

        await FollowersService.AddAsync(user1.UserId, user2.UserId, OperationContext.None());

        for (var i = 0; i < 2; i++)
        {
            await FollowersService.RemoveAsync(user1.UserId, user2.UserId, OperationContext.None());

            var followers1 = await FollowersService.GetFollowersAsync(user1.UserId, OperationContext.None());
            Assert.That(followers1, Is.Empty);

            var followings1 = await FollowersService.GetFollowingsAsync(user1.UserId, OperationContext.None());
            Assert.That(followings1, Is.Empty);

            var followers2 = await FollowersService.GetFollowersAsync(user2.UserId, OperationContext.None());
            Assert.That(followers2, Is.Empty);

            var followings2 = await FollowersService.GetFollowingsAsync(user2.UserId, OperationContext.None());
            Assert.That(followings2, Is.Empty);
        }
    }

    private async Task<UserSession> CreateUserAsync()
    {
        var userName = Guid.NewGuid().ToString("N");
        await AccountService.RegisterAsync($"{userName}@xxx.com", userName, "Display"+userName, "pass", OperationContext.None());
        var profile = await AccountService.LoginWithPasswordAsync($"{userName}@xxx.com", "pass", OperationContext.None());
        
        return profile != null ? new UserSession(profile.UserId, profile.DisplayName, profile.Handle) : throw new InvalidOperationException("Cannot find user");
    }
}