using System.Security.Claims;
using SocialApp.WebApi.Features._Shared.Services;
using SocialApp.WebApi.Features.Session.Models;
using SocialApp.WebApi.Features.Session.Services;

namespace SocialApp.WebApi.Infrastructure;

public class SessionManager
{
    private readonly ISessionService _sessions;
    private readonly IHttpContextAccessor _http;

    public SessionManager(ISessionService sessions, IHttpContextAccessor http)
    {
        _sessions = sessions;
        _http = http;
    }

    public async Task<UserSession?> LoadUserSessionAsync(OperationContext context)
    {
        var http = _http.HttpContext;
        
        var sessionId = http?.User.Claims.Where(x => x.Type == ClaimTypes.Sid).Select(x => x.Value).SingleOrDefault();

        if (sessionId == null)
            return null;
        return await _sessions.GetSessionAsync(sessionId, context);
    }

    public async Task UpdateSessionAsync(UserSession? session, OperationContext context)
    {
        if(session == null)
            return;
        
        await _sessions.UpdateSessionAsync(session.SessionId, context);
    }
}