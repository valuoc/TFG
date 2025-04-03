using SocialApp.WebApi.Data.User;
using SocialApp.WebApi.Features._Shared.Services;
using SocialApp.WebApi.Features.Content.Containers;
using SocialApp.WebApi.Features.Content.Models;
using SocialApp.WebApi.Features.Session.Models;

namespace SocialApp.WebApi.Features.Content.Services;

public sealed class FeedService
{
    private readonly UserDatabase _userDb;
    
    public FeedService(UserDatabase userDb)
        => _userDb = userDb;

    private FeedContainer GetFeedContainer()
        => new(_userDb);
    
    public async Task<IReadOnlyList<ThreadHeaderModel>> GetFeedAsync(UserSession session, OperationContext context)
    {
        var feeds = GetFeedContainer();
        var (threads, threadCounts) = await feeds.GetUserFeedDocumentsAsync(session.UserId, null, 10, context);
        
        var sorted = threads
            .Join(threadCounts, i => i.ThreadId, o => o.ThreadId, (i, o) => (i, o))
            .OrderBy(x => x.i.Sk);
        
        var postsModels = new List<ThreadHeaderModel>(threads.Count);
        foreach (var (thread, counts) in sorted)
        {
            var post = ThreadHeaderModel.From(thread);
            post.CommentCount = counts.CommentCount;
            post.ViewCount = counts.ViewCount;
            post.LikeCount = counts.LikeCount;
            postsModels.Add(post);
        }
        return postsModels;
    }
}