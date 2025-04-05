using SocialApp.WebApi.Data.Shared;

namespace SocialApp.WebApi.Data.Session;

public record SessionDocument(string SessionId, string UserId, string DisplayName, string Handle) 
    : Document(Key(SessionId))
{
    public static DocumentKey Key(string sessionId)
    {
        var pk = "session:"+sessionId;
        return new DocumentKey(pk, "session");
    }
}