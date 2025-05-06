using SocialApp.Models.Content;
using SocialApp.WebApi.Data.User;
using SocialApp.WebApi.Features._Shared.Services;
using SocialApp.WebApi.Features._Shared.Tuples;
using SocialApp.WebApi.Features.Account.Services;
using SocialApp.WebApi.Features.Content.Containers;
using SocialApp.WebApi.Features.Content.Queries;
using SocialApp.WebApi.Features.Session.Models;

namespace SocialApp.WebApi.Features.Content.Services;

public interface IFeedService
{
    Task<IReadOnlyList<ConversationRoot>> GetFeedAsync(UserSession session, string? beforeConversationId, OperationContext context);
}

public sealed class FeedService : IFeedService
{
    private readonly IQueries _queries;
    private readonly UserDatabase _userDb;
    private readonly IUserHandleService _userHandleService;

    public FeedService(UserDatabase userDb, IUserHandleService userHandleService, IQueries queries)
    {
        _userDb = userDb;
        _userHandleService = userHandleService;
        _queries = queries;
    }

    private FeedContainer GetFeedContainer()
        => new(_userDb);
    
    public async Task<IReadOnlyList<ConversationRoot>> GetFeedAsync(UserSession session, string? beforeConversationId, OperationContext context)
    {
        var feeds = GetFeedContainer();

        var query = new UserFeedQuery
        {
            UserId = session.UserId,
            BeforeConversationId = beforeConversationId,
            Limit = 10
        };

        var list = new List<FeedConversationTuple>();
        await foreach (var conversation in _queries.ExecuteQueryManyAsync<UserFeedQuery, FeedConversationTuple>(feeds, query, context))
        {
            list.Add(conversation);
        }
        
        var sorted = list
            .OrderByDescending(x => x.Conversation.Sk);
        
        var conversationsModels = new List<ConversationRoot>(list.Count);
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
            Handle = await _userHandleService.GetHandleAsync(conversation.FeedUserId, context),
            ConversationId = conversation.ConversationId,
            Content = conversation.Content,
            LastModify = conversation.LastModify,
            ViewCount = counts.ViewCount,
            CommentCount = counts.CommentCount,
            LikeCount = counts.LikeCount
        };
}