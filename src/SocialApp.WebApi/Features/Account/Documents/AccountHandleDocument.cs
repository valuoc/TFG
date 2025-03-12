using SocialApp.WebApi.Features.Documents;

namespace SocialApp.WebApi.Features.Account.Documents;

public record AccountHandleDocument(string Handle, string UserId) 
    : Document(Key(Handle))
{
    public static DocumentKey Key(string handle)
    {
        var pk = "handle:"+handle;
        return new DocumentKey(pk, "handle");
    }
}