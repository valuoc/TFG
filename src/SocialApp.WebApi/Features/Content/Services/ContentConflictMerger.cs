using SocialApp.WebApi.Data._Shared;
using SocialApp.WebApi.Data.User;
using SocialApp.WebApi.Features._Shared.Services;
using SocialApp.WebApi.Features.Content.Containers;

namespace SocialApp.WebApi.Features.Content.Services;

public interface IConflictMerger
{
    Task<bool> MergeAsync(Document? remoteConflict, Document? localConflict, OperationContext context);
}

public class ContentConflictMerger : IConflictMerger
{
    private readonly ContentContainer _container;
    public ContentConflictMerger(ContentContainer container)
        => _container = container;

    public async Task<bool> MergeAsync(Document? remoteConflict, Document? localConflict, OperationContext context)
    {
        if (remoteConflict is null)
            return true;
        if (localConflict is null)
            return true;

        if (remoteConflict.GetType() != localConflict.GetType())
        {
            // Likely a bug?
            return false;
        }
        
        switch (remoteConflict)
        {
            case ConversationDocument remoteConversation:
                if(TryMergeConversation(remoteConversation, (ConversationDocument)localConflict, out var merged))
                {
                    var uow = _container.CreateUnitOfWork(merged.Pk);
                    uow.Update(merged!);
                    await uow.SaveChangesAsync(context);
                    return true;
                }

                break;
                    
            case ConversationCountsDocument remoteCounts:
                var (success, mergedCounts) = await TryMergeConversationCounts(remoteCounts, (ConversationCountsDocument)localConflict, context);
                if(success)
                {
                    var uow = _container.CreateUnitOfWork(mergedCounts.Pk);
                    uow.Update(mergedCounts!);
                    await uow.SaveChangesAsync(context);
                    return true;
                }

                break;
        }

        return false;
    }

    private async Task<(bool, ConversationCountsDocument)> TryMergeConversationCounts(ConversationCountsDocument remoteConflict, ConversationCountsDocument localConflict, OperationContext context)
    {
        var conversationCountKey = ConversationCountsDocument.Key(localConflict.UserId, localConflict.ConversationId);
        var current = await _container.GetAsync<ConversationCountsDocument>(conversationCountKey, context);
        
        if(current == null)
            return (false, remoteConflict);
        
        var merged = current with
        {
            CommentCount = (remoteConflict.CommentCount - current.CommentCount) + (localConflict.CommentCount - current.CommentCount),
            ViewCount = (remoteConflict.ViewCount - current.ViewCount) + (localConflict.ViewCount - current.ViewCount),
            LikeCount = (remoteConflict.LikeCount - current.LikeCount) + (localConflict.LikeCount - current.LikeCount)
        };
        return (true, merged);
    }

    private bool TryMergeConversation(ConversationDocument remoteConflict, ConversationDocument localConflict, out ConversationDocument? merged)
    {
        merged = remoteConflict.LastModify > localConflict.LastModify 
            ? remoteConflict 
            : localConflict;

        return true;
    }
}