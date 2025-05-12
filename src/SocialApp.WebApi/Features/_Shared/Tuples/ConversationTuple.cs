using SocialApp.WebApi.Data.User;

namespace SocialApp.WebApi.Features._Shared.Tuples;

public record ConversationTuple(ConversationDocument Conversation, ConversationCountsDocument Counts);