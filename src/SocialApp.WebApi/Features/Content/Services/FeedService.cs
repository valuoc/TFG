using SocialApp.Models.Content;
using SocialApp.WebApi.Data.User;
using SocialApp.WebApi.Features._Shared.Services;
using SocialApp.WebApi.Features.Account.Services;
using SocialApp.WebApi.Features.Content.Containers;
using SocialApp.WebApi.Features.Session.Models;

namespace SocialApp.WebApi.Features.Content.Services;

public interface IFeedService
{
    Task<IReadOnlyList<ConversationRoot>> GetFeedAsync(UserSession session, string? beforeConversationId, OperationContext context);
}

public sealed class FeedService : IFeedService
{
    private readonly UserDatabase _userDb;
    private readonly IUserHandleService _userHandleService;

    public FeedService(UserDatabase userDb, IUserHandleService userHandleService)
    {
        _userDb = userDb;
        _userHandleService = userHandleService;
    }

    private FeedContainer GetFeedContainer()
        => new(_userDb);
    
    public async Task<IReadOnlyList<ConversationRoot>> GetFeedAsync(UserSession session, string? beforeConversationId, OperationContext context)
    {
        var feeds = GetFeedContainer();
        var (conversations, conversationCounts) = await feeds.GetUserFeedDocumentsAsync(session.UserId, beforeConversationId, 10, context);
        
        var sorted = conversations
            .Join(conversationCounts, i => i.ConversationId, o => o.ConversationId, (i, o) => (i, o))
            .OrderByDescending(x => x.i.Sk);
        
        var conversationsModels = new List<ConversationRoot>(conversations.Count);
        foreach (var (conversationDoc, countsDoc) in sorted)
        {
            var conversation = await FeedConversationAsync(conversationDoc, countsDoc, context);
            conversationsModels.Add(conversation);
        }
        return conversationsModels;
    }
    
    private async Task<ConversationRoot> FeedConversationAsync(FeedConversationDocument conversation, FeedConversationCountsDocument counts, OperationContext context)
        => new()
        {
            Handle = await _userHandleService.GetHandleFromUserIdAsync(conversation.FeedUserId, context),
            ConversationId = conversation.ConversationId,
            Content = conversation.Content,
            LastModify = conversation.LastModify,
            ViewCount = counts.ViewCount,
            CommentCount = counts.CommentCount,
            LikeCount = counts.LikeCount
        };
}