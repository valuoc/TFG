using SocialApp.Models.Content;
using SocialApp.WebApi.Data.User;
using SocialApp.WebApi.Features._Shared.Services;
using SocialApp.WebApi.Features.Content.Containers;
using SocialApp.WebApi.Features.Session.Models;

namespace SocialApp.WebApi.Features.Content.Services;

public interface IFeedService
{
    Task<IReadOnlyList<ConversationHeaderModel>> GetFeedAsync(UserSession session, string? afterConversationId, OperationContext context);
}

public sealed class FeedService : IFeedService
{
    private readonly UserDatabase _userDb;
    
    public FeedService(UserDatabase userDb)
        => _userDb = userDb;

    private FeedContainer GetFeedContainer()
        => new(_userDb);
    
    public async Task<IReadOnlyList<ConversationHeaderModel>> GetFeedAsync(UserSession session, string? afterConversationId, OperationContext context)
    {
        var feeds = GetFeedContainer();
        var (conversations, conversationCounts) = await feeds.GetUserFeedDocumentsAsync(session.UserId, afterConversationId, 5, context);
        
        var sorted = conversations
            .Join(conversationCounts, i => i.ConversationId, o => o.ConversationId, (i, o) => (i, o))
            .OrderByDescending(x => x.i.Sk);
        
        var conversationsModels = new List<ConversationHeaderModel>(conversations.Count);
        foreach (var (conversationDoc, countsDoc) in sorted)
        {
            var conversation = ContentModels.From(conversationDoc);
            conversation.CommentCount = countsDoc.CommentCount;
            conversation.ViewCount = countsDoc.ViewCount;
            conversation.LikeCount = countsDoc.LikeCount;
            conversationsModels.Add(conversation);
        }
        return conversationsModels;
    }
}