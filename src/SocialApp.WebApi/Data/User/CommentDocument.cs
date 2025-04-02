using SocialApp.WebApi.Data._Shared;

namespace SocialApp.WebApi.Data.User;

public record CommentDocument(string ThreadUserId, string ThreadId, string UserId, string CommentId, string Content, DateTime LastModify, int Version) 
    : Document(Key(ThreadUserId, ThreadId, CommentId))
{
    public static DocumentKey Key(string threadUserId, string threadId, string commentId)
    {
        var pk = "user:"+threadUserId;
        var id = $"thread:{threadId}:comment:{commentId}";
        return new DocumentKey(pk, id);
    }
}