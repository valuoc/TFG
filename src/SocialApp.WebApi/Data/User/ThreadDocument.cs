using SocialApp.WebApi.Data._Shared;

namespace SocialApp.WebApi.Data.User;

public record ThreadDocument(string UserId, string ThreadId, string Content, DateTime LastModify, int Version, string? ParentThreadUserId, string? ParentThreadId) 
    : Document(Key(UserId, ThreadId))
{
    public bool IsRootThread {get; set;}

    public static DocumentKey Key(string userId, string threadId)
    {
        var pk = "user:"+userId;
        var id = $"thread:{threadId}:thread";
        return new DocumentKey(pk, id);
    }
    
    public static DocumentKey KeyUserThreadsEnd(string userId)
    {
        var pk = "user:"+userId;
        var id = "thread:z";
        return new DocumentKey(pk, id);
    }
    
    public static DocumentKey KeyThreadsItemsStart(string userId, string threadId)
    {
        var pk = "user:"+userId;
        var id = $"thread:{threadId}";
        return new DocumentKey(pk, id);
    }

    public static DocumentKey KeyThreadItemsEnd(string userId, string threadId)
    {
        var pk = "user:"+userId;
        var id = $"thread:{threadId}:z"; // z as limit
        return new DocumentKey(pk, id);
    }
}