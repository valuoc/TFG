using SocialApp.WebApi.Features._Shared.Services;

namespace SocialApp.Tests.ServicesTests;

[Order(1)]
public class FeedServiceTests : ServiceTestsBase
{
    [Test, Order(1)]
    public async Task Feed_Is_Populated()
    {
        var user1 = await CreateUserAsync("user1");
        var user2 = await CreateUserAsync("user2");
        var user3 = await CreateUserAsync("user3");
        
        await FollowersService.AddAsync(user1.UserId, user2.UserId, OperationContext.New());
        await FollowersService.AddAsync(user1.UserId, user3.UserId, OperationContext.New());

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
            await ContentService.CreateThreadAsync(user2, moments[i].ToString(), context);
            
            context = new OperationContext(CancellationToken.None);
            context.SetTime(now.AddSeconds(moments[i+1]));
            await ContentService.CreateThreadAsync(user3, moments[i+1].ToString(), context);
        }

        await Task.Delay(5_000);
        
        var feed = await FeedService.GetFeedAsync(user1, OperationContext.New());
        Assert.That(feed, Is.Not.Null);
        Assert.That(feed, Is.Not.Empty);
    }
    
    [Test, Order(1)]
    public async Task Feed_Is_Updated()
    {
        var user1 = await CreateUserAsync("user1");
        var user2 = await CreateUserAsync("user2");
        var user3 = await CreateUserAsync("user3");
        
        await FollowersService.AddAsync(user1.UserId, user2.UserId, OperationContext.New());
        await FollowersService.AddAsync(user1.UserId, user3.UserId, OperationContext.New());

        var now = DateTimeOffset.UtcNow;
        
        var moments = Enumerable
            .Range(0, 10)
            .Select(i => i + 1)
            .OrderBy(x => Guid.NewGuid())
            .ToArray();

        var user2ThreadIds = new List<string>();
        var user3ThreadIds = new List<string>();
        
        for (var i = 0; i < moments.Length; i+=2)
        {
            var context = new OperationContext(CancellationToken.None);
            context.SetTime(now.AddSeconds(moments[i]));
            user2ThreadIds.Add(await ContentService.CreateThreadAsync(user2, moments[i].ToString(), context));
            
            context = new OperationContext(CancellationToken.None);
            context.SetTime(now.AddSeconds(moments[i+1]));
            user3ThreadIds.Add(await ContentService.CreateThreadAsync(user3, moments[i+1].ToString(), context));
        }

        await Task.Delay(5_000);
        
        var feed = await FeedService.GetFeedAsync(user1, OperationContext.New());
        Assert.That(feed, Is.Not.Null);
        Assert.That(feed, Is.Not.Empty);
        Assert.That(feed.Count, Is.EqualTo(10));

        var user2ThreadId = user2ThreadIds.OrderBy(x => Guid.NewGuid()).First();
        var user3ThreadId = user3ThreadIds.OrderBy(x => Guid.NewGuid()).First();

        await ContentService.UpdateThreadAsync(user2, user2ThreadId, "Updated !!", OperationContext.New());
        await ContentService.DeleteThreadAsync(user3, user3ThreadId, OperationContext.New());
        
        await Task.Delay(5_000);
        
        feed = await FeedService.GetFeedAsync(user1, OperationContext.New());
        Assert.That(feed, Is.Not.Null);
        Assert.That(feed, Is.Not.Empty);
        Assert.That(feed.Count, Is.EqualTo(9));
        var updatedFeed = feed.Single(x => x.ThreadId == user2ThreadId);
        Assert.That(updatedFeed.Content, Is.EqualTo("Updated !!"));
    }
}