namespace SocialApp.WebApi.Features.Session.Models;

public record UserSession(string UserId, string SessionId, string DisplayName, string Handle)
{
    private HashSet<string> _pendingOperationIds;
    
    public void RegisterPendingOperation(string pendingId)
    {
        _pendingOperationIds ??= new HashSet<string>();
        _pendingOperationIds.Add(pendingId);
    }
    
    public void CompletePendingOperation(string pendingId)
    {
        if (_pendingOperationIds == null)
            throw new InvalidOperationException("Pending operations are empty!");
        if(!_pendingOperationIds.Remove(pendingId))
            throw new InvalidOperationException("Pending operations not found!");
    }
}