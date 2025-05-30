using SocialApp.Models.Content;
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
        
        await FollowersService.FollowAsync(user1, user2.Handle, OperationContext.New());
        await FollowersService.FollowAsync(user1, user3.Handle, OperationContext.New());

        var now = DateTimeOffset.UtcNow;
        
        var moments = Enumerable
            .Range(0, 20)
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
        Assert.That(feed.Count, Is.EqualTo(10));
        for (var i = 0; i < feed.Count; i++)
            Assert.That(feed[i].Content, Is.EqualTo((20-i).ToString()));
        
        feed = await FeedService.GetFeedAsync(user1, feed.Last().ConversationId, OperationContext.New());
        Assert.That(feed, Is.Not.Null);
        Assert.That(feed, Is.Not.Empty);
        Assert.That(feed.Count, Is.EqualTo(10));
        for (var i = 0; i < feed.Count; i++)
            Assert.That(feed[i].Content, Is.EqualTo((10-i).ToString()));
        
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
        
        await FollowersService.FollowAsync(user1, user2.Handle, OperationContext.New());
        await FollowersService.FollowAsync(user1, user3.Handle, OperationContext.New());

        var now = DateTimeOffset.UtcNow;
        
        var moments = Enumerable
            .Range(0, 20)
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

        List<ConversationRoot> feed = new List<ConversationRoot>();
        feed.AddRange(await FeedService.GetFeedAsync(user1, null, OperationContext.New()));
        Assert.That(feed, Is.Not.Null);
        Assert.That(feed, Is.Not.Empty);
        Assert.That(feed.Count, Is.EqualTo(10));
        feed.AddRange(await FeedService.GetFeedAsync(user1, feed.Last().ConversationId, OperationContext.New()));
        Assert.That(feed.Count, Is.EqualTo(20));

        var user2UpdatedConversationId = user2ConversationIds.OrderBy(x => Guid.NewGuid()).First();
        var user3DeletedConversationId = user3ConversationIds.OrderBy(x => Guid.NewGuid()).First();

        await ContentService.UpdateConversationAsync(user2, user2UpdatedConversationId, "Updated !!", OperationContext.New());
        await ContentService.DeleteConversationAsync(user3, user3DeletedConversationId, OperationContext.New());
        await ContentService.ReactToConversationAsync(user1, user2.Handle, user2UpdatedConversationId, true, OperationContext.New());
        
        await Task.Delay(5_000);
        
        feed.Clear();
        feed.AddRange(await FeedService.GetFeedAsync(user1, null, OperationContext.New()));
        feed.AddRange(await FeedService.GetFeedAsync(user1, feed.Last().ConversationId, OperationContext.New()));
        Assert.That(feed, Is.Not.Null);
        Assert.That(feed, Is.Not.Empty);
        Assert.That(feed.Count, Is.EqualTo(19));
        var updatedFeed = feed.Single(x => x.ConversationId == user2UpdatedConversationId);
        Assert.That(updatedFeed.Content, Is.EqualTo("Updated !!"));
        Assert.That(updatedFeed.LikeCount, Is.EqualTo(1));
    }
}