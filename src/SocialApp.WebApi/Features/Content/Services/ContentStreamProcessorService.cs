using System.Net;
using Microsoft.Azure.Cosmos;
using SocialApp.WebApi.Data.User;
using SocialApp.WebApi.Features._Shared.Services;
using SocialApp.WebApi.Features.Content.Containers;
using SocialApp.WebApi.Features.Content.Exceptions;
using SocialApp.WebApi.Features.Follow.Containers;

namespace SocialApp.WebApi.Features.Content.Services;

public sealed class ContentStreamProcessorService
{
    private readonly UserDatabase _userDb;
    
    private  int FeedItemTtl => (int)TimeSpan.FromDays(2).TotalSeconds;
    
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
                var context = new OperationContext(c);
                var error = StreamProcessingError.UnexpectedError;
                try
                {
                    switch (document)
                    {
                        case ThreadDocument doc:
                            error = StreamProcessingError.ThreadToParentComment;
                            await SyncThreadToParentCommentAsync(contents, doc, context);
                            error = StreamProcessingError.ThreadToFeed;
                            await PropagateThreadToFollowersFeedsAsync(GetFeedContainer(), follows, doc, context);
                            break;
                
                        case ThreadCountsDocument doc:
                            error = StreamProcessingError.ThreadCountToParentComment;
                            await SyncThreadCountsToParentCommentAsync(contents, doc, context);
                            error = StreamProcessingError.ThreadCountToFeed;
                            await PropagateThreadCountsToFollowersFeedAsync(GetFeedContainer(), follows, doc, context);
                            break;
                
                        case CommentDocument doc:
                            error = StreamProcessingError.VerifyChildThreadCreation;
                            await EnsureChildThreadIsCreatedOnCommentAsync(contents, doc, context);
                            break;
                
                        case CommentCountsDocument doc:
                            break;
                    }
                }
                catch (Exception e)
                {
                    throw new StreamProcessingException(error, document.Pk, document.Id, e);
                }
            });
        }
    }
    
    private async Task SyncThreadToParentCommentAsync(ContentContainer contents, ThreadDocument thread, OperationContext context)
    {
        if(thread.IsRootThread)
            return; // It has no parent comment
        
        var comment = await contents.GetCommentAsync(thread.ParentThreadUserId, thread.ParentThreadId, thread.ThreadId, context);
        if(comment == null)
        {
            if(thread.Deleted)
                return;
            
            // Comment was not created
            await contents.CreateCommentAsync(new CommentDocument(thread.ParentThreadUserId, thread.ParentThreadId, thread.UserId, thread.ThreadId, thread.Content, thread.LastModify, thread.Version), context);
        }
        else if (thread.Deleted && !comment.Deleted)
        {
            // Delete comment and counts
            await contents.RemoveCommentAsync(comment.ThreadUserId, comment.ThreadId, comment.CommentId, context);
        }
        else if (comment.Version < thread.Version)
        {
            // Comment is outdated
            await contents.ReplaceDocumentAsync(comment with
            {
                Content = thread.Content, 
                Version = thread.Version
            }, context);
        }
    }
    
    private async Task SyncThreadCountsToParentCommentAsync(ContentContainer contents, ThreadCountsDocument tcounts, OperationContext context)
    {
        if(tcounts.IsRootThread)
            return; // It has no parent comment
        
        if(tcounts.AllCountersAreZero() && !tcounts.Deleted) // It is the thread created as a consequence of the comment
            return;

        var ccounts = CommentCountsDocument.TryGenerateParentCommentCounts(tcounts);
        if(ccounts == null)
            return;
        
        await contents.ReplaceDocumentAsync(ccounts, context);
    }

    private async Task EnsureChildThreadIsCreatedOnCommentAsync(ContentContainer contents, CommentDocument comment, OperationContext context)
    {
        var (thread,_) = await contents.GetPostDocumentAsync(comment.UserId, comment.CommentId, context);
        if(thread != null)
            return;

        thread = new ThreadDocument(comment.UserId, comment.CommentId, comment.Content, comment.LastModify, comment.Version, comment.ThreadUserId, comment.ThreadId);
        try
        {
            await contents.CreateThreadAsync(thread, context);
        }
        catch (Exception e) when (e.GetBaseException() is CosmosException { StatusCode: HttpStatusCode.Conflict })
        {
            // This can happen if the Change Feed is catching up with the main thread
        }
    }

    private async Task PropagateThreadCountsToFollowersFeedAsync(FeedContainer contents, FollowContainer follows, ThreadCountsDocument counts, OperationContext context)
    {
        var followers2 = await follows.GetFollowersAsync(counts.UserId, context);
        foreach (var followerId in GetFollowersAsync(followers2))
        {
            var feedItem = FeedThreadCountsDocument.From(followerId, counts) with
            {
                Ttl = FeedItemTtl,
                Deleted = counts.Deleted,
            };
            await contents.SaveFeedItemAsync(feedItem, context);
        }
    }

    private async Task PropagateThreadToFollowersFeedsAsync(FeedContainer container, FollowContainer follows, ThreadDocument thread, OperationContext context)
    {
        var followers1 = await follows.GetFollowersAsync(thread.UserId, context);
        foreach (var followerId in GetFollowersAsync(followers1))
        {
            var feedItem = FeedThreadDocument.From(followerId, thread) with
            {
                Ttl = FeedItemTtl,
                Deleted = thread.Deleted
            };
            await container.SaveFeedItemAsync(feedItem, context);
        }
    }

    private static HashSet<string> GetFollowersAsync(FollowerListDocument? followers)
        => followers?.Followers??[];
}