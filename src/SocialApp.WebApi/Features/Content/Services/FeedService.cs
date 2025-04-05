using SocialApp.WebApi.Data.User;
using SocialApp.WebApi.Features._Shared.Services;
using SocialApp.WebApi.Features.Content.Containers;
using SocialApp.WebApi.Features.Content.Models;
using SocialApp.WebApi.Features.Session.Models;

namespace SocialApp.WebApi.Features.Content.Services;

public interface IFeedService
{
    Task<IReadOnlyList<ThreadHeaderModel>> GetFeedAsync(UserSession session, string? afterThreadId, OperationContext context);
}

public sealed class FeedService : IFeedService
{
    private readonly UserDatabase _userDb;
    
    public FeedService(UserDatabase userDb)
        => _userDb = userDb;

    private FeedContainer GetFeedContainer()
        => new(_userDb);
    
    public async Task<IReadOnlyList<ThreadHeaderModel>> GetFeedAsync(UserSession session, string? afterThreadId, OperationContext context)
    {
        var feeds = GetFeedContainer();
        var (threads, threadCounts) = await feeds.GetUserFeedDocumentsAsync(session.UserId, afterThreadId, 5, context);
        
        var sorted = threads
            .Join(threadCounts, i => i.ThreadId, o => o.ThreadId, (i, o) => (i, o))
            .OrderByDescending(x => x.i.Sk);
        
        var threadsModels = new List<ThreadHeaderModel>(threads.Count);
        foreach (var (threadDoc, countsDoc) in sorted)
        {
            var thread = ThreadHeaderModel.From(threadDoc);
            thread.CommentCount = countsDoc.CommentCount;
            thread.ViewCount = countsDoc.ViewCount;
            thread.LikeCount = countsDoc.LikeCount;
            threadsModels.Add(thread);
        }
        return threadsModels;
    }
}