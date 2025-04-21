using Microsoft.Azure.Cosmos;
using SocialApp.WebApi.Data._Shared;
using SocialApp.WebApi.Data.Session;
using SocialApp.WebApi.Features._Shared.Services;
using SocialApp.WebApi.Features.Session.Models;

namespace SocialApp.WebApi.Features.Session.Containers;

public sealed class SessionContainer : CosmoContainer
{
    private static readonly ItemRequestOptions _noResponseContent = new(){ EnableContentResponseOnWrite = false};

    public SessionContainer(SessionDatabase database)
        :base(database, "sessions")
    { }
    
    public async Task CreateSessionAsync(SessionDocument session, OperationContext context)
    {
        var response = await Container.CreateItemAsync(session, requestOptions: _noResponseContent, cancellationToken: context.Cancellation);
        context.AddRequestCharge(response.RequestCharge);
    }
    
    public async Task<UserSession?> GetSessionAsync(string sessionId, int sessionLengthSeconds, OperationContext context)
    {
        var sessionKey = SessionDocument.Key(sessionId);
        var response = await Container.ReadItemAsync<SessionDocument>(sessionKey.Id, new PartitionKey(sessionKey.Pk), cancellationToken: context.Cancellation);
        context.AddRequestCharge(response.RequestCharge);
        if (response?.Resource == null)
            return null;

        var sessionDocument = response.Resource;
        if(sessionDocument.Ttl < sessionLengthSeconds / 4) // TODO: Patch ?
        {
            response = await Container.ReplaceItemAsync(sessionDocument with { Ttl = sessionLengthSeconds}, sessionId, requestOptions:_noResponseContent, cancellationToken: context.Cancellation);
            context.AddRequestCharge(response.RequestCharge);
        }

        return new UserSession(sessionDocument.UserId, sessionId, sessionDocument.DisplayName, sessionDocument.Handle);
    }
    
    public async Task EndSessionAsync(string sessionId, OperationContext context)
    {
        var sessionKey = SessionDocument.Key(sessionId);
        var response = await Container.DeleteItemAsync<SessionDocument>(sessionKey.Id, new PartitionKey(sessionKey.Pk), requestOptions: _noResponseContent, cancellationToken: context.Cancellation);
        context.AddRequestCharge(response.RequestCharge);
    }
}