using SocialApp.WebApi.Data._Shared;

namespace SocialApp.WebApi.Data.User;

public record HandleDocument(string Handle, string UserId) 
    : Document(Key(Handle))
{
    public static DocumentKey Key(string handle)
    {
        var pk = "handle:"+handle;
        return new DocumentKey(pk, "handle");
    }
}