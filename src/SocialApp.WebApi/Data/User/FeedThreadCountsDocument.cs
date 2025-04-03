using SocialApp.WebApi.Data._Shared;

namespace SocialApp.WebApi.Data.User;

public record FeedThreadCountsDocument(string FeedUserId, string ThreadUserId, string ThreadId, int LikeCount, int CommentCount, int ViewCount) 
    : Document(Key(FeedUserId, ThreadUserId, ThreadId))
{
    public static FeedThreadCountsDocument From(string feedUserId, ThreadCountsDocument thread)
        => new(feedUserId, thread.UserId, thread.ThreadId, thread.LikeCount, thread.CommentCount, thread.ViewCount)
        {
            Deleted = thread.Deleted
        };

    public static DocumentKey Key(string feedUserId, string threaUserId, string threadId)
    {
        var pk = "user:"+feedUserId;
        var id = $"feed:{threadId}:{threaUserId}:thread_counts";
        return new DocumentKey(pk, id);
    }
}