using System.Net;
using Microsoft.Azure.Cosmos;
using SocialApp.WebApi.Features.Services;
using SocialApp.WebApi.Features.Session.Databases;
using SocialApp.WebApi.Features.Session.Documents;
using SocialApp.WebApi.Features.Session.Exceptions;

namespace SocialApp.WebApi.Features.Session.Services;

public record User(string UserId, string DisplayName, string Handle);

public class SessionService
{
    private static readonly ItemRequestOptions _noResponseContent = new(){ EnableContentResponseOnWrite = false};
    private int _sessionLengthSeconds = 60*60;
    
    private readonly SessionDatabase _sessionDatabase;

    public SessionService(SessionDatabase sessionDatabase)
    {
        _sessionDatabase = sessionDatabase;
    }

    public async ValueTask<string> StarSessionAsync(User user, OperationContext context)
    {
        var session = new SessionDocument(Guid.NewGuid().ToString("N"), user.UserId, user.DisplayName, user.Handle)
        {
            Ttl = _sessionLengthSeconds
        };
        try
        {
            var sessions = _sessionDatabase.GetSessionContainer();
            await sessions.CreateItemAsync(session, requestOptions: _noResponseContent, cancellationToken: context.Cancellation);
            return session.SessionId;
        }
        catch (CosmosException e)
        {
            throw new SessionSocialAppException(SessionError.UnexpectedError, e);
        }
    }

    public async ValueTask<User?> GetSessionAsync(string sessionId, OperationContext context)
    {
        try
        {
            var sessions = _sessionDatabase.GetSessionContainer();
            var sessionKey = SessionDocument.Key(sessionId);
            var response = await sessions.ReadItemAsync<SessionDocument>(sessionKey.Id, new PartitionKey(sessionKey.Pk), cancellationToken: context.Cancellation);
            if (response?.Resource == null)
                return null;
        
            if(response.Resource.Ttl < _sessionLengthSeconds / 4) // TODO: Patch ?
                await sessions.ReplaceItemAsync(response.Resource with { Ttl = _sessionLengthSeconds}, sessionId, requestOptions:_noResponseContent, cancellationToken: context.Cancellation);
        
            return new User(response.Resource.UserId, response.Resource.DisplayName, response.Resource.Handle);
        }
        catch (CosmosException e) when (e.StatusCode == HttpStatusCode.NotFound)
        {
            throw new SessionSocialAppException(SessionError.InvalidSession, e);
        }
        catch (CosmosException e)
        {
            throw new SessionSocialAppException(SessionError.UnexpectedError, e);
        }
    }

    public async ValueTask EndSessionAsync(string sessionId, OperationContext context)
    {
        try
        {
            var sessions = _sessionDatabase.GetSessionContainer();
            var sessionKey = SessionDocument.Key(sessionId);
            await sessions.DeleteItemAsync<SessionDocument>(sessionKey.Id, new PartitionKey(sessionKey.Pk), requestOptions: _noResponseContent, cancellationToken: context.Cancellation);

        }
        catch (CosmosException e) when (e.StatusCode == HttpStatusCode.NotFound)
        {

        }
        catch (CosmosException e)
        {
            throw new SessionSocialAppException(SessionError.UnexpectedError, e);
        }
    }
}