using System.Net;
using Microsoft.Azure.Cosmos;
using SocialApp.WebApi.Features.Account.Databases;
using SocialApp.WebApi.Features.Services;
using SocialApp.WebApi.Features.Session.Databases;
using SocialApp.WebApi.Features.Session.Documents;
using SocialApp.WebApi.Features.Session.Exceptions;
using SocialApp.WebApi.Features.Session.Models;

namespace SocialApp.WebApi.Features.Session.Services;

public class SessionService
{
    private int _sessionLengthSeconds = 60*60;
    
    private readonly UserDatabase _userDd;
    private readonly SessionDatabase _sessionDb;

    public SessionService(UserDatabase userDd, SessionDatabase sessionDb)
    {
        _userDd = userDd;
        _sessionDb = sessionDb;
    }
    
    private SessionContainer GetSessionContainer()
        => new(_sessionDb);
    
    private ProfileContainer GetProfileContainer()
        => new(_userDd);

    public async ValueTask<UserSession?> LoginWithPasswordAsync(string email, string password, OperationContext context)
    {
        try
        {
            var sessions = GetSessionContainer();
            var userId = await sessions.FindPasswordLoginAsync(email, password, context);
            if (userId == null)
                return null;

            var profile = await GetProfileContainer().GetProfileAsync(userId, context);

            if (profile == null)
                return null;
            
            var session = new SessionDocument(Guid.NewGuid().ToString("N"), userId, profile.DisplayName, profile.Handle)
            {
                Ttl = _sessionLengthSeconds
            };
            
            await sessions.CreateSessionAsync(session, context);
            return new UserSession(userId, session.SessionId, profile.DisplayName, profile.Handle);
        }
        catch (CosmosException e) when (e.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
        catch (CosmosException e)
        {
            throw new SessionException(SessionError.UnexpectedError, e);
        }
    }
    
    public async ValueTask<UserSession?> GetSessionAsync(string sessionId, OperationContext context)
    {
        try
        {
            var sessions = GetSessionContainer();
            return await sessions.GetSessionAsync(sessionId, _sessionLengthSeconds, context);
        }
        catch (CosmosException e) when (e.StatusCode == HttpStatusCode.NotFound)
        {
            throw new SessionException(SessionError.InvalidSession, e);
        }
        catch (CosmosException e)
        {
            throw new SessionException(SessionError.UnexpectedError, e);
        }
    }

    public async ValueTask EndSessionAsync(string sessionId, OperationContext context)
    {
        try
        {
            var sessions = GetSessionContainer();
            await sessions.EndSessionAsync(sessionId, context);
        }
        catch (CosmosException e) when (e.StatusCode == HttpStatusCode.NotFound)
        {

        }
        catch (CosmosException e)
        {
            throw new SessionException(SessionError.UnexpectedError, e);
        }
    }
}