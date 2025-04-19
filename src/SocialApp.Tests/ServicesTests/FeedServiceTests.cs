using SocialApp.WebApi.Features._Shared.Services;
using SocialApp.WebApi.Features.Content.Models;

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
        
        await FollowersService.FollowAsync(user1.UserId, user2.UserId, OperationContext.New());
        await FollowersService.FollowAsync(user1.UserId, user3.UserId, OperationContext.New());

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
            await ContentService.StartConversationAsync(user2, moments[i].ToString(), context);
            
            context = new OperationContext(CancellationToken.None);
            context.SetTime(now.AddSeconds(moments[i+1]));
            await ContentService.StartConversationAsync(user3, moments[i+1].ToString(), context);
        }

        await Task.Delay(5_000);
        
        var feed = await FeedService.GetFeedAsync(user1, null, OperationContext.New());
        Assert.That(feed, Is.Not.Null);
        Assert.That(feed, Is.Not.Empty);
        Assert.That(feed.Count, Is.EqualTo(5));
        for (var i = 0; i < feed.Count; i++)
            Assert.That(feed[i].Content, Is.EqualTo((10-i).ToString()));
        
        feed = await FeedService.GetFeedAsync(user1, feed.Last().ConversationId, OperationContext.New());
        Assert.That(feed, Is.Not.Null);
        Assert.That(feed, Is.Not.Empty);
        Assert.That(feed.Count, Is.EqualTo(5));
        for (var i = 0; i < feed.Count; i++)
            Assert.That(feed[i].Content, Is.EqualTo((5-i).ToString()));
        
        feed = await FeedService.GetFeedAsync(user1, feed.Last().ConversationId, OperationContext.New());
        Assert.That(feed, Is.Not.Null);
        Assert.That(feed, Is.Empty);
    }
    
    [Test, Order(1)]
    public async Task Feed_Is_Updated()
    {
        var user1 = await CreateUserAsync("user1");
        var user2 = await CreateUserAsync("user2");
        var user3 = await CreateUserAsync("user3");
        
        await FollowersService.FollowAsync(user1.UserId, user2.UserId, OperationContext.New());
        await FollowersService.FollowAsync(user1.UserId, user3.UserId, OperationContext.New());

        var now = DateTimeOffset.UtcNow;
        
        var moments = Enumerable
            .Range(0, 10)
            .Select(i => i + 1)
            .OrderBy(x => Guid.NewGuid())
            .ToArray();

        var user2ConversationIds = new List<string>();
        var user3ConversationIds = new List<string>();
        
        for (var i = 0; i < moments.Length; i+=2)
        {
            var context = new OperationContext(CancellationToken.None);
            context.SetTime(now.AddSeconds(moments[i]));
            user2ConversationIds.Add(await ContentService.StartConversationAsync(user2, moments[i].ToString(), context));
            
            context = new OperationContext(CancellationToken.None);
            context.SetTime(now.AddSeconds(moments[i+1]));
            user3ConversationIds.Add(await ContentService.StartConversationAsync(user3, moments[i+1].ToString(), context));
        }

        await Task.Delay(5_000);

        List<ConversationHeaderModel> feed = new List<ConversationHeaderModel>();
        feed.AddRange(await FeedService.GetFeedAsync(user1, null, OperationContext.New()));
        Assert.That(feed, Is.Not.Null);
        Assert.That(feed, Is.Not.Empty);
        Assert.That(feed.Count, Is.EqualTo(5));
        feed.AddRange(await FeedService.GetFeedAsync(user1, feed.Last().ConversationId, OperationContext.New()));
        Assert.That(feed.Count, Is.EqualTo(10));

        var user2UpdatedConversationId = user2ConversationIds.OrderBy(x => Guid.NewGuid()).First();
        var user3DeletedConversationId = user3ConversationIds.OrderBy(x => Guid.NewGuid()).First();

        await ContentService.UpdateConversationAsync(user2, user2UpdatedConversationId, "Updated !!", OperationContext.New());
        await ContentService.DeleteConversationAsync(user3, user3DeletedConversationId, OperationContext.New());
        await ContentService.ReactToConversationAsync(user1, user2.UserId, user2UpdatedConversationId, true, OperationContext.New());
        
        await Task.Delay(5_000);
        
        feed.Clear();
        feed.AddRange(await FeedService.GetFeedAsync(user1, null, OperationContext.New()));
        feed.AddRange(await FeedService.GetFeedAsync(user1, feed.Last().ConversationId, OperationContext.New()));
        Assert.That(feed, Is.Not.Null);
        Assert.That(feed, Is.Not.Empty);
        Assert.That(feed.Count, Is.EqualTo(9));
        var updatedFeed = feed.Single(x => x.ConversationId == user2UpdatedConversationId);
        Assert.That(updatedFeed.Content, Is.EqualTo("Updated !!"));
        Assert.That(updatedFeed.LikeCount, Is.EqualTo(1));
    }
}