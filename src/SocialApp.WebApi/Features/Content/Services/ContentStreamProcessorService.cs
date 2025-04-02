using SocialApp.WebApi.Data.User;
using SocialApp.WebApi.Features._Shared.Services;
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
                    case ThreadDocument doc:
                        await PropagateThreadToFollowersFeedsAsync(GetFeedContainer(), follows, doc, c);
                        await PropagateThreadToCommentAsync(contents, doc, c);
                        break;
                
                    case ThreadCountsDocument doc:
                        await PropagateThreadCountsToCommentAsync(contents, doc, c);
                        await PropagateThreadCountsToFollowersFeedAsync(GetFeedContainer(), follows, doc, c);
                        break;
                
                    case CommentDocument doc:
                        await PropagateCommentAsThreadAsync(contents, doc, c);
                        break;
                
                    case CommentCountsDocument doc:
                        break;
                }
            });
        }
    }

    private async Task PropagateThreadToCommentAsync(ContentContainer contents, ThreadDocument thread, CancellationToken cancel)
    {
        if(thread.IsRootThread)
            return;
        var context = new OperationContext(cancel);
        var comment = await contents.GetCommentAsync(thread.ParentThreadUserId, thread.ParentThreadId, thread.ThreadId, context);
        if(comment == null)
            await contents.CreateCommentAsync(new CommentDocument(thread.ParentThreadUserId, thread.ParentThreadId, thread.UserId, thread.ThreadId, thread.Content, thread.LastModify, thread.Version), context);
        else if (comment.Version != thread.Version)
            await contents.ReplaceDocumentAsync(comment with { Content = thread.Content, Version = thread.Version }, context);
    }

    private async Task PropagateCommentAsThreadAsync(ContentContainer contents, CommentDocument comment, CancellationToken cancel)
    {
        var (thread,_) = await contents.GetPostDocumentAsync(comment.UserId, comment.CommentId, new OperationContext(cancel));
        if(thread != null)
            return;
        thread = new ThreadDocument(comment.UserId, comment.CommentId, comment.Content, comment.LastModify, comment.Version, comment.ThreadUserId, comment.ThreadId);
        await contents.CreateThreadAsync(thread, new OperationContext(cancel));
    }

    private async Task PropagateThreadCountsToCommentAsync(ContentContainer contents, ThreadCountsDocument tcounts, CancellationToken cancel)
    {
        if(tcounts.IsRootThread)
            return;
        
        if(tcounts.CommentCount == 0) // It is the thread created as a consequence of the comment
            return;

        var ccounts = new CommentCountsDocument(tcounts.ParentThreadUserId, tcounts.ParentThreadId, tcounts.UserId, tcounts.ThreadId,
            tcounts.LikeCount, tcounts.CommentCount, tcounts.ViewCount);
        
        await contents.ReplaceDocumentAsync(ccounts, new OperationContext(cancel));
    }
    
    private static async Task PropagateThreadCountsToFollowersFeedAsync(FeedContainer contents, FollowContainer follows, ThreadCountsDocument doc, CancellationToken cancel)
    {
        var followers2 = await follows.GetFollowersAsync(doc.UserId, cancel);
        foreach (var followerId in GetFollowersAsync(followers2))
        {
            var feedItem = FeedThreadCountsDocument.From(followerId, doc) with { Ttl = (int)TimeSpan.FromDays(2).TotalSeconds};
            await contents.SaveFeedItemAsync(feedItem, cancel);
        }
    }

    private static async Task PropagateThreadToFollowersFeedsAsync(FeedContainer container, FollowContainer follows, ThreadDocument doc, CancellationToken cancel)
    {
        var followers1 = await follows.GetFollowersAsync(doc.UserId, cancel);
        foreach (var followerId in GetFollowersAsync(followers1))
        {
            var feedItem = FeedThreadDocument.From(followerId, doc) with { Ttl = (int)TimeSpan.FromDays(2).TotalSeconds};
            await container.SaveFeedItemAsync(feedItem, cancel);
        }
    }

    private static HashSet<string> GetFollowersAsync(FollowerListDocument? followers)
        => followers?.Followers??[];
}