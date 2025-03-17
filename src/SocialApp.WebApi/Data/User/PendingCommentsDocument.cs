using SocialApp.WebApi.Data._Shared;

namespace SocialApp.WebApi.Data.User;

public enum PendingCommentOperation { Add, Update, Delete}
public record PendingComment(string UserId, string PostId, string ParentUserId, string ParentPostId, PendingCommentOperation Operation);
public record PendingCommentsDocument(string UserId) 
    : Document(Key(UserId))
{
    public PendingComment[] Items { get; set; } = [];
    
    public static DocumentKey Key(string userId)
    {
        var pk = "user:"+userId;
        var id = "pending_posts";
        return new DocumentKey(pk, id);
    }
}