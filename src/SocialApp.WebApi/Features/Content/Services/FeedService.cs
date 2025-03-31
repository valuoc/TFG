using SocialApp.WebApi.Data.User;
using SocialApp.WebApi.Features._Shared.Services;
using SocialApp.WebApi.Features.Content.Containers;
using SocialApp.WebApi.Features.Content.Models;
using SocialApp.WebApi.Features.Follow.Containers;
using SocialApp.WebApi.Features.Session.Models;

namespace SocialApp.WebApi.Features.Content.Services;

public sealed class FeedService
{
    private readonly UserDatabase _userDb;
    
    public FeedService(UserDatabase userDb)
        => _userDb = userDb;

    private FeedContainer GetFeedContainer()
        => new(_userDb);
    
    private FollowContainer GetFollowContainer()
        => new(_userDb);

    public async Task<IReadOnlyList<Post>> GetFeedAsync(UserSession session, OperationContext context)
    {
        var feeds = GetFeedContainer();
        var documents = await feeds.GetUserFeedAsync(session.UserId, null, 10, context);
        
        var postsModels = new List<Post>(documents.Count);
        foreach (var (postDoc, countsDoc) in documents)
        {
            var post = Post.From(postDoc);
            post.CommentCount = countsDoc.CommentCount;
            post.ViewCount = countsDoc.ViewCount;
            post.LikeCount = countsDoc.LikeCount;
            postsModels.Add(post);
        }
        return postsModels;
    }

    public async Task ProcessChangeFeedAsync(CancellationToken cancel)
    {
        var container = GetFeedContainer();
        var ranges = await container.GetFeedRangesAsync();
        var tasks = new List<Task>();
        
        foreach (var range in ranges)
            tasks.Add(Task.Run(() => ProcessRangeAsync(container, range, cancel), cancel));
        
        await Task.WhenAll(tasks);
    }

    private async Task ProcessRangeAsync(FeedContainer container, string range, CancellationToken cancel)
    {
        static HashSet<string> GetFollowersAsync(FollowerListDocument? followers)
            => followers?.Followers??[];

        var follows = GetFollowContainer();
        await foreach (var document in container.ReadFeedAsync(range, null, cancel))
        {
            switch (document)
            {
                case PostDocument doc:
                    var followers1 = await follows.GetFollowersAsync(doc.UserId, cancel);
                    foreach (var followerId in GetFollowersAsync(followers1))
                    {
                        var feedItem = FeedPostDocument.From(followerId, doc) with { Ttl = (int)TimeSpan.FromDays(2).TotalSeconds};
                        await container.SaveFeedItemAsync(feedItem, cancel);
                    }
                    break;
                
                case PostCountsDocument doc:
                    var followers2 = await follows.GetFollowersAsync(doc.UserId, cancel);
                    foreach (var followerId in GetFollowersAsync(followers2))
                    {
                        var feedItem = FeedPostCountsDocument.From(followerId, doc) with { Ttl = (int)TimeSpan.FromDays(2).TotalSeconds};
                        await container.SaveFeedItemAsync(feedItem, cancel);
                    }
                    break;
                
                case CommentDocument doc:
                    break;
                
                case CommentCountsDocument doc:
                    break;
            }
        }
    }
}