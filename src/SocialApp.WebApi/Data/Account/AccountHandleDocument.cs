using SocialApp.WebApi.Data._Shared;

namespace SocialApp.WebApi.Data.Account;

public record AccountHandleDocument(string Handle, string UserId) 
    : Document(Key(Handle))
{
    public static DocumentKey Key(string handle)
    {
        var pk = "handle:"+handle;
        return new DocumentKey(pk, "handle");
    }
}