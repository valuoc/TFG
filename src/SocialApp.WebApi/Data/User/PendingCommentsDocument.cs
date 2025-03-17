using SocialApp.WebApi.Data._Shared;
namespace SocialApp.WebApi.Data.User;

public record PendingOperation(string Id, string Name, DateTime CreationDate, string[] Data);
public record PendingOperationsDocument(string UserId) 
    : Document(Key(UserId))
{
    public PendingOperation[] Items { get; set; } = [];
    
    public static DocumentKey Key(string userId)
    {
        var pk = "user:"+userId;
        var id = "pending_operations";
        return new DocumentKey(pk, id);
    }
}