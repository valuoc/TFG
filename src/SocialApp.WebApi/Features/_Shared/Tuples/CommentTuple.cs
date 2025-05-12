using SocialApp.WebApi.Data.User;

namespace SocialApp.WebApi.Features._Shared.Tuples;

public record CommentTuple(CommentDocument Comment, CommentCountsDocument Counts);