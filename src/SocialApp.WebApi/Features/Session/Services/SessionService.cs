using SocialApp.Models.Session;
using SocialApp.WebApi.Data.Session;
using SocialApp.WebApi.Data.User;
using SocialApp.WebApi.Features._Shared.Services;
using SocialApp.WebApi.Features.Account.Containers;
using SocialApp.WebApi.Features.Account.Queries;
using SocialApp.WebApi.Features.Session.Containers;
using SocialApp.WebApi.Features.Session.Exceptions;
using SocialApp.WebApi.Features.Session.Models;

namespace SocialApp.WebApi.Features.Session.Services;

public interface ISessionService
{
    Task<UserSession?> LoginWithPasswordAsync(LoginRequest request, OperationContext context);
    Task<UserSession?> GetSessionAsync(string sessionId, OperationContext context);
    Task EndSessionAsync(UserSession session, OperationContext context);
}

public class SessionService : ISessionService
{
    private int _sessionLengthSeconds = 60*60;
    
    private readonly UserDatabase _userDd;
    private readonly SessionDatabase _sessionDb;
    private readonly IQueries _queries;

    public SessionService(UserDatabase userDd, SessionDatabase sessionDb, IQueries queries)
    {
        _userDd = userDd;
        _sessionDb = sessionDb;
        _queries = queries;
    }

    private SessionContainer GetSessionContainer()
        => new(_sessionDb);
    
    private ProfileContainer GetProfileContainer()
        => new(_userDd);

    public async Task<UserSession?> LoginWithPasswordAsync(LoginRequest request, OperationContext context)
    {
        try
        {
            var profiles = GetProfileContainer();
            var key = PasswordLoginDocument.Key(request.Email);
            var login = await profiles.GetAsync<PasswordLoginDocument>(key, context);
            if (login == null || login.Password != Passwords.HashPassword(request.Password))
                return null;
            
            var profile = await _queries.QuerySingleAsync(profiles, new ProfileQuery() { UserId = login.UserId }, context);

            if (profile == null)
                return null;
            
            var session = new SessionDocument(Guid.NewGuid().ToString("N"), login.UserId, profile.DisplayName, profile.Handle)
            {
                Ttl = _sessionLengthSeconds
            };

            var sessions = GetSessionContainer();
            var uow = sessions.CreateUnitOfWork(session.Pk);
            uow.Create(session);
            await uow.SaveChangesAsync(context);
            return new UserSession(login.UserId, session.SessionId, profile.DisplayName, profile.Handle);
        }
        catch (Exception e)
        {
            throw new SessionException(SessionError.UnexpectedError, e);
        }
    }
    
    public async Task<UserSession?> GetSessionAsync(string sessionId, OperationContext context)
    {
        try
        {
            var sessions = GetSessionContainer();
            
            var sessionKey = SessionDocument.Key(sessionId);
            var sessionDocument = await sessions.GetAsync<SessionDocument>(sessionKey, context);
 
            if (sessionDocument == null)
                return null;
            
            if(sessionDocument.Ttl < _sessionLengthSeconds / 4) // TODO: Patch ?
            {
                sessionDocument = sessionDocument with { Ttl = _sessionLengthSeconds };
                var uow = sessions.CreateUnitOfWork(sessionDocument.Pk);
                uow.Update(sessionDocument);
                await uow.SaveChangesAsync(context);
            }

            return new UserSession(sessionDocument.UserId, sessionId, sessionDocument.DisplayName, sessionDocument.Handle);
            
        }
        catch (Exception e)
        {
            throw new SessionException(SessionError.UnexpectedError, e);
        }
    }

    public async Task EndSessionAsync(UserSession session, OperationContext context)
    {
        try
        {
            var sessions = GetSessionContainer();
            var sessionKey = SessionDocument.Key(session.SessionId);
            var uow = sessions.CreateUnitOfWork(sessionKey.Pk);
            uow.Delete<SessionDocument>(sessionKey);
            await uow.SaveChangesAsync(context);
        }
        catch (UnitOfWorkException ex) when (ex.Error == OperationError.NotFound){}
        catch (Exception e)
        {
            throw new SessionException(SessionError.UnexpectedError, e);
        }
    }
}