using SocialApp.WebApi.Features.Services;
using SocialApp.WebApi.Features.Session.Services;

namespace SocialApp.Tests.ServicesTests;

[Order(2)]
public class FollowServiceTests: ServiceTestsBase
{
    [Test, Order(1)]
    public async Task Follows_Can_Be_Managed()
    {
        var user1 = await CreateUserAsync();
        var user2 = await CreateUserAsync();

        await FollowersService.AddAsync(user1.UserId, user2.UserId, OperationContext.None());
        
        //var followers = await FollowersService.GetFollowersAsync(user2.UserId, OperationContext.None());
        //var followings = await FollowersService.GetFollowingsAsync(user1.UserId, OperationContext.None());
        
        await FollowersService.RemoveAsync(user1.UserId, user2.UserId, OperationContext.None());
    }

    private async Task<User> CreateUserAsync()
    {
        var userName = Guid.NewGuid().ToString("N");
        var id1 = await AccountService.RegisterAsync($"{userName}@xxx.com", userName, "Display"+userName, "pass", OperationContext.None());
        var user = await AccountService.LoginWithPasswordAsync($"{userName}@xxx.com", "pass", OperationContext.None());
        return user ?? throw new InvalidOperationException("Cannot find user");
    }
}