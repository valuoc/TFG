using SocialApp.WebApi.Data._Shared;

namespace SocialApp.WebApi.Data.Account;

public record HandleLockDocument(string Handle, string UserId) 
    : Document(Key(Handle))
{
    public static DocumentKey Key(string handle)
    {
        var pk = "handle_lock:"+handle;
        return new DocumentKey(pk, "handle");
    }
}