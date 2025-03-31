using SocialApp.WebApi.Features._Shared.Services;

namespace SocialApp.Tests.ServicesTests;

[Order(4)]
public class FeedServiceTests : ServiceTestsBase
{
    [Test, Order(1)]
    public async Task Feed_Is_Populated()
    {
        var user1 = await CreateUserAsync("user1");
        var user2 = await CreateUserAsync("user2");
        var user3 = await CreateUserAsync("user3");
        
        await FollowersService.AddAsync(user1.UserId, user2.UserId, OperationContext.None());
        await FollowersService.AddAsync(user1.UserId, user3.UserId, OperationContext.None());

        var now = DateTimeOffset.UtcNow;
        
        var moments = Enumerable
            .Range(0, 10)
            .Select(i => i + 1)
            .OrderBy(x => Guid.NewGuid())
            .ToArray();

        for (var i = 0; i < moments.Length; i+=2)
        {
            var context = new OperationContext(CancellationToken.None);
            context.SetTime(now.AddSeconds(moments[i]));
            await ContentService.CreatePostAsync(user2, moments[i].ToString(), context);
            
            context = new OperationContext(CancellationToken.None);
            context.SetTime(now.AddSeconds(moments[i+1]));
            await ContentService.CreatePostAsync(user3, moments[i+1].ToString(), context);
        }

        await Task.Delay(5_000);
        
        var feed = await FeedService.GetFeedAsync(user1, OperationContext.None());
        Assert.That(feed, Is.Not.Null);
        Assert.That(feed, Is.Not.Empty);
    }
}