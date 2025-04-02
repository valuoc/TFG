using SocialApp.WebApi.Data._Shared;

namespace SocialApp.WebApi.Data.User;

public record ThreadCountsDocument(string UserId, string ThreadId, int LikeCount, int CommentCount, int ViewCount, string? ParentThreadUserId, string? ParentThreadId) 
    : Document(Key(UserId, ThreadId))
{
    public bool IsRootThread {get; set;}
    
    public static DocumentKey Key(string userId, string postId)
    {
        var pk = "user:"+userId;
        var id = $"thread:{postId}:thread_counts";
        return new DocumentKey(pk, id);
    }
}