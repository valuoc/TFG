using SocialApp.WebApi.Data.User;

namespace SocialApp.WebApi.Features._Shared.Tuples;

public record FeedConversationTuple(FeedConversationDocument Conversation, FeedConversationCountsDocument Counts);