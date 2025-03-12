using SocialApp.WebApi.Features.Documents;

namespace SocialApp.WebApi.Features.Session.Documents;

public record SessionDocument(string SessionId, string UserId, string DisplayName, string Handle) 
    : Document(Key(SessionId))
{
    public static DocumentKey Key(string sessionId)
    {
        var pk = "session:"+sessionId;
        return new DocumentKey(pk, "session");
    }
}