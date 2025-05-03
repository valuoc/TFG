using System.Net;
using Microsoft.Azure.Cosmos;
using SocialApp.WebApi.Data._Shared;
using SocialApp.WebApi.Data.User;
using SocialApp.WebApi.Features._Shared.Services;
using SocialApp.WebApi.Features.Content.Containers;
using SocialApp.WebApi.Features.Content.Exceptions;
using SocialApp.WebApi.Features.Follow.Containers;

namespace SocialApp.WebApi.Features.Content.Services;

public interface IContentStreamProcessorService
{
    Task ProcessChangeFeedAsync(CancellationToken cancel);
}

public sealed class ContentStreamProcessorService : IContentStreamProcessorService
{
    private readonly UserDatabase _userDb;
    private readonly ILogger<ContentStreamProcessorService> _logger;

    private  int FeedItemTtl => (int)TimeSpan.FromDays(2).TotalSeconds;
    
    public ContentStreamProcessorService(UserDatabase userDb, ILogger<ContentStreamProcessorService> logger)
    {
        _userDb = userDb;
        _logger = logger;
    }

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
                        case ConversationDocument doc:
                            error = StreamProcessingError.ConversationToParentComment;
                            await SyncConversationToParentCommentAsync(contents, doc, context);
                            error = StreamProcessingError.ConversationToFeed;
                            await PropagateConversationToFollowersFeedsAsync(GetFeedContainer(), follows, doc, context);
                            break;

                        case ConversationCountsDocument doc:
                            error = StreamProcessingError.ConversationCountToParentComment;
                            await SyncConversationCountsToParentCommentAsync(contents, doc, context);
                            error = StreamProcessingError.ConversationCountToFeed;
                            await PropagateConversationCountsToFollowersFeedAsync(GetFeedContainer(), follows, doc, context);
                            break;

                        case CommentDocument doc:
                            error = StreamProcessingError.VerifyChildConversationCreation;
                            await EnsureChildConversationIsCreatedOnCommentAsync(contents, doc, context);
                            break;

                        case ConversationUserLikeDocument doc:
                            error = StreamProcessingError.VerifyLikePropagation;
                            await EnsureLikeHasPropagatedAsync(contents, doc, context);
                            break;
                        
                        case CommentCountsDocument doc:
                            break;
                        
                        default:
                            //_logger.LogWarning("Content processor processing unexpected document: '{fullName}'", document.GetType().FullName);
                            break;
                    }
                }
                catch (CosmosException e)
                {
                    context.AddRequestCharge(e.RequestCharge);
                    throw new StreamProcessingException(error, document.Pk, document.Id, e);
                }
                catch (Exception e)
                {
                    throw new StreamProcessingException(error, document.Pk, document.Id, e);
                }
                finally
                {
                    ReportOperationCharge(document, context);
                }
            });
        }
    }

    private async Task EnsureLikeHasPropagatedAsync(ContentContainer contents, ConversationUserLikeDocument doc, OperationContext context)
    {
        var conversationLike = await contents.GetConversationReactionAsync(doc.ConversationUserId, doc.ConversationId, doc.UserId, context);
        if (conversationLike == null || conversationLike.Like != doc.Like)
        {
            conversationLike = new ConversationLikeDocument(doc.ConversationUserId, doc.ConversationId, doc.UserId, doc.Like)
            {
                Ttl = doc.Like ? -1 : (int)TimeSpan.FromDays(2).TotalSeconds
            };
            await contents.ReactConversationAsync(conversationLike, context);
        }
        
        if(string.IsNullOrWhiteSpace(doc.ParentConversationUserId))
            return;

        var commentLike = await contents.GetCommentReactionAsync(doc.ParentConversationUserId, doc.ParentConversationId, doc.ConversationId, doc.UserId, context);
        if (commentLike == null || commentLike.Like != doc.Like)
        {
            commentLike = new CommentLikeDocument(doc.ParentConversationUserId, doc.ParentConversationId, doc.ConversationId, doc.UserId, doc.Like)
            {
                Ttl = doc.Like ? -1 : (int)TimeSpan.FromDays(2).TotalSeconds
            }; 
            await contents.ReactCommentAsync(commentLike, context);
        }
    } 

    private void ReportOperationCharge(Document document, OperationContext context)
    {
        if(context.OperationCharge == 0)
            return;
        
        _logger.LogInformation($"Handle({document.GetType().Name}): {context.OperationCharge}");
    }

    private async Task SyncConversationToParentCommentAsync(ContentContainer contents, ConversationDocument conversation, OperationContext context)
    {
        if(conversation.IsRootConversation)
            return; // It has no parent comment
        
        var comment = await contents.GetCommentAsync(conversation.ParentConversationUserId, conversation.ParentConversationId, conversation.ConversationId, context);
        if(comment == null)
        {
            if(conversation.Deleted)
                return;
            
            // Comment was not created
            await contents.CreateCommentAsync(new CommentDocument(conversation.ParentConversationUserId, conversation.ParentConversationId, conversation.UserId, conversation.ConversationId, conversation.Content, conversation.LastModify, conversation.Version), context);
        }
        else if (conversation.Deleted && !comment.Deleted)
        {
            // Delete comment and counts
            await contents.RemoveCommentAsync(comment.ConversationUserId, comment.ConversationId, comment.CommentId, context);
        }
        else if (comment.Version < conversation.Version)
        {
            // Comment is outdated
            await contents.UpdateAsync(comment with
            {
                Content = conversation.Content, 
                Version = conversation.Version
            }, context);
        }
    }
    
    private async Task SyncConversationCountsToParentCommentAsync(ContentContainer contents, ConversationCountsDocument tcounts, OperationContext context)
    {
        if(tcounts.IsRootConversation)
            return; // It has no parent comment
        
        if(tcounts.AllCountersAreZero() && !tcounts.Deleted) // It is the conversation created as a consequence of the comment
            return;

        var ccounts = CommentCountsDocument.TryGenerateParentCommentCounts(tcounts);
        if(ccounts == null)
            return;
        
        await contents.UpdateAsync(ccounts, context);
    }

    private async Task EnsureChildConversationIsCreatedOnCommentAsync(ContentContainer contents, CommentDocument comment, OperationContext context)
    {
        var conversation = await contents.GetConversationDocumentAsync(comment.UserId, comment.CommentId, context);
        if(conversation != null)
            return;

        conversation = new ConversationDocument(comment.UserId, comment.CommentId, comment.Content, comment.LastModify, comment.Version, comment.ConversationUserId, comment.ConversationId);
        try
        {
            await contents.CreateConversationAsync(conversation, context);
        }
        catch (Exception e) when (e.GetBaseException() is CosmosException { StatusCode: HttpStatusCode.Conflict })
        {
            // This can happen if the Change Feed is catching up with the main conversation
        }
    }
    
    private static async Task<FollowerListDocument?> GetFollowerListAsync(FollowContainer container, string userId, OperationContext context)
    {
        var followerKey = FollowerListDocument.Key(userId);
        var followerList = await container.GetAsync<FollowerListDocument>(followerKey, context);
        return followerList;
    }


    private async Task PropagateConversationCountsToFollowersFeedAsync(FeedContainer contents, FollowContainer follows, ConversationCountsDocument counts, OperationContext context)
    {
        var followers2 = await GetFollowerListAsync(follows, counts.UserId, context);
        foreach (var followerId in GetFollowersAsync(followers2))
        {
            var feedItem = FeedConversationCountsDocument.From(followerId, counts) with
            {
                Ttl = FeedItemTtl,
                Deleted = counts.Deleted,
            };
            await contents.SaveFeedItemAsync(feedItem, context);
        }
    }

    private async Task PropagateConversationToFollowersFeedsAsync(FeedContainer container, FollowContainer follows, ConversationDocument conversation, OperationContext context)
    {
        var followers1 = await GetFollowerListAsync(follows, conversation.UserId, context);
        foreach (var followerId in GetFollowersAsync(followers1))
        {
            var feedItem = FeedConversationDocument.From(followerId, conversation) with
            {
                Ttl = FeedItemTtl,
                Deleted = conversation.Deleted
            };
            await container.SaveFeedItemAsync(feedItem, context);
        }
    }

    private static HashSet<string> GetFollowersAsync(FollowerListDocument? followers)
        => followers?.Followers??[];
}