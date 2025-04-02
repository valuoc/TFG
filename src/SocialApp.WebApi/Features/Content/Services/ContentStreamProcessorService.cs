using SocialApp.WebApi.Data.User;
using SocialApp.WebApi.Features.Content.Containers;
using SocialApp.WebApi.Features.Follow.Containers;

namespace SocialApp.WebApi.Features.Content.Services;

public sealed class ContentStreamProcessorService
{
    private readonly UserDatabase _userDb;
    
    public ContentStreamProcessorService(UserDatabase userDb)
        => _userDb = userDb;

    private FeedContainer GetFeedContainer()
        => new(_userDb);
    
    private FollowContainer GetFollowContainer()
        => new(_userDb);
    
    private ContentContainer GetContentContainer()
        => new(_userDb);
    
    public async Task ProcessChangeFeedAsync(CancellationToken cancel)
    {
        var contents = GetContentContainer();
        var ranges = await contents.GetFeedRangesAsync();
        var tasks = new List<Task>();
        
        foreach (var range in ranges)
            tasks.Add(Task.Run(() => ProcessRangeAsync(contents, range, cancel), cancel));
        
        await Task.WhenAll(tasks);
    }

    private async Task ProcessRangeAsync(ContentContainer contents, string range, CancellationToken cancel)
    {
        var follows = GetFollowContainer();
        await foreach (var (documents, continuation) in contents.ReadFeedAsync(range, null, cancel))
        {
            await Parallel.ForEachAsync(documents, cancel, async (document, c) =>
            {
                switch (document)
                {
                    case PostDocument doc:
                        await PropagatePostToFollowersFeedsAsync(GetFeedContainer(), follows, doc, c);
                        break;
                
                    case PostCountsDocument doc:
                        await PropagatePostCountsToCommentAsync(contents, doc, c);
                        await PropagatePostCountsToFollowersFeedAsync(GetFeedContainer(), follows, doc, c);
                        break;
                
                    case CommentDocument doc:
                        //await PropagateCommentAsync(contents, follows, doc, c);
                        break;
                
                    case CommentCountsDocument doc:
                        //await PropagateCommentCountsAsync(doc, c);
                        break;
                }
            });
        }
    }

    private async Task PropagatePostCountsToCommentAsync(ContentContainer contents, PostCountsDocument counts, CancellationToken cancel)
    {

    }
    
    private static async Task PropagatePostCountsToFollowersFeedAsync(FeedContainer contents, FollowContainer follows, PostCountsDocument doc, CancellationToken cancel)
    {
        var followers2 = await follows.GetFollowersAsync(doc.UserId, cancel);
        foreach (var followerId in GetFollowersAsync(followers2))
        {
            var feedItem = FeedPostCountsDocument.From(followerId, doc) with { Ttl = (int)TimeSpan.FromDays(2).TotalSeconds};
            await contents.SaveFeedItemAsync(feedItem, cancel);
        }
    }

    private static async Task PropagatePostToFollowersFeedsAsync(FeedContainer container, FollowContainer follows, PostDocument doc, CancellationToken cancel)
    {
        var followers1 = await follows.GetFollowersAsync(doc.UserId, cancel);
        foreach (var followerId in GetFollowersAsync(followers1))
        {
            var feedItem = FeedPostDocument.From(followerId, doc) with { Ttl = (int)TimeSpan.FromDays(2).TotalSeconds};
            await container.SaveFeedItemAsync(feedItem, cancel);
        }
    }

    private static HashSet<string> GetFollowersAsync(FollowerListDocument? followers)
        => followers?.Followers??[];
}