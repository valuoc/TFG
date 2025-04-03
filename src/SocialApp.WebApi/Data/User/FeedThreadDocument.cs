using SocialApp.WebApi.Data._Shared;

namespace SocialApp.WebApi.Data.User;

public record FeedThreadDocument(string FeedUserId, string ThreadUserId, string ThreadId, string Content, DateTime LastModify, int Version) 
    : Document(Key(FeedUserId, ThreadUserId, ThreadId))
{
    public static FeedThreadDocument From(string feedUserId, ThreadDocument thread)
        => new(feedUserId, thread.UserId, thread.ThreadId, thread.Content, thread.LastModify, thread.Version)
        {
            Deleted = thread.Deleted
        };

    public static DocumentKey Key(string userId, string threadUserId, string threadId)
    {
        var pk = "user:"+userId;
        var id = $"feed:{threadId}:{threadUserId}:thread";
        return new DocumentKey(pk, id);
    }
    
    public static DocumentKey KeyUserFeedEnd(string userId)
    {
        var pk = "user:"+userId;
        var id = "feed:z";
        return new DocumentKey(pk, id);
    }
    
    public static DocumentKey KeyUserFeedStart(string userId)
    {
        var pk = "user:"+userId;
        var id = $"feed:";
        return new DocumentKey(pk, id);
    }
    
    public static DocumentKey KeyUserFeedFrom(string userId, string threadId)
    {
        var pk = "user:"+userId;
        var id = $"feed:{threadId}:";
        return new DocumentKey(pk, id);
    }
}