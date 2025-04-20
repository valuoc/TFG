using System.Security.Claims;
using SocialApp.WebApi.Features._Shared.Services;
using SocialApp.WebApi.Features.Session.Models;
using SocialApp.WebApi.Features.Session.Services;

namespace SocialApp.WebApi.Infrastructure;

public class SessionGetter
{
    private readonly ISessionService _sessions;
    private readonly IHttpContextAccessor _http;
    public SessionGetter(ISessionService sessions, IHttpContextAccessor http)
    {
        _sessions = sessions;
        _http = http;
    }

    public async Task<UserSession?> GetUserSessionAsync(OperationContext context)
    {
        var http = _http.HttpContext;
        
        var sessionId = http?.User.Claims.Where(x => x.Type == ClaimTypes.Sid).Select(x => x.Value).SingleOrDefault();

        if (sessionId == null)
            return null;
        var session =  await _sessions.GetSessionAsync(sessionId, context);
        return session;
    }
}