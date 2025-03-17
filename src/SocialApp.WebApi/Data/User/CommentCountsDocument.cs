using SocialApp.WebApi.Data._Shared;

namespace SocialApp.WebApi.Data.User;

public record CommentCountsDocument(string UserId, string PostId, string ParentUserId, string ParentPostId, int LikeCount, int CommentCount, int ViewCount) 
    : Document(Key(ParentUserId, ParentPostId, PostId))
{
    public static DocumentKey Key(string parentUserId, string parentPostId, string commentId)
    {
        var pk = "user:"+parentUserId;
        var id = $"post:{parentPostId}:comment:{commentId}:counts";
        return new DocumentKey(pk, id);
    }
}