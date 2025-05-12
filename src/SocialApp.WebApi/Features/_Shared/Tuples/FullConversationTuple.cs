namespace SocialApp.WebApi.Features._Shared.Tuples;

public record FullConversationTuple(ConversationTuple ConversationTuple, IReadOnlyList<CommentTuple> Comments);