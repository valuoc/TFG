using SocialApp.WebApi.Data._Shared;

namespace SocialApp.WebApi.Data.User;

public record CommentDocument(string UserId, string PostId, string ParentUserId, string ParentPostId, string Content, DateTime LastModify, int Version) 
    : Document(Key(ParentUserId, ParentPostId, PostId))
{
    public static DocumentKey Key(string parentUserId, string parentPostId, string commentId)
    {
        var pk = "user:"+parentUserId;
        var id = $"post:{parentPostId}:comment:{commentId}";
        return new DocumentKey(pk, id);
    }
}