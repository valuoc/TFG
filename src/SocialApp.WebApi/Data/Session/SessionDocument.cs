using SocialApp.WebApi.Data._Shared;

namespace SocialApp.WebApi.Data.Session;

public record SessionDocument(string SessionId, string UserId, string DisplayName, string Handle, Dictionary<string,string>? SessionTokens = null) 
    : Document(Key(SessionId))
{
    public static DocumentKey Key(string sessionId)
    {
        var pk = "session:"+sessionId;
        return new DocumentKey(pk, "session");
    }
}