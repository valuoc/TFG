using System.Net;
using Microsoft.Azure.Cosmos;
using SocialApp.WebApi.Data.Shared;
using SocialApp.WebApi.Data.Session;
using SocialApp.WebApi.Features._Shared.Services;
using SocialApp.WebApi.Features.Session.Models;

namespace SocialApp.WebApi.Features.Session.Containers;

public sealed class SessionContainer : CosmoContainer
{
    private static readonly ItemRequestOptions _noResponseContent = new(){ EnableContentResponseOnWrite = false};

    public SessionContainer(SessionDatabase database)
        :base(database)
    { }
    
    public async Task CreatePasswordLoginAsync(string userId, string email, string password, OperationContext context)
    {
        var passwordLogin = new PasswordLoginDocument(userId, email, Passwords.HashPassword(password));
        var response = await Container.CreateItemAsync(passwordLogin,  requestOptions: _noResponseContent, cancellationToken: context.Cancellation);
        context.AddRequestCharge(response.RequestCharge);
    }

    public async Task<string?> FindPasswordLoginAsync(string email, string password, OperationContext context)
    {
        var loginKey = PasswordLoginDocument.Key(email);
        var response = await Container.ReadItemAsync<PasswordLoginDocument>(loginKey.Id, new PartitionKey(loginKey.Pk), cancellationToken: context.Cancellation);
        context.AddRequestCharge(response.RequestCharge);
        if (response.Resource == null || response.Resource.Password != Passwords.HashPassword(password))
        {
            return null;
        }
        return response.Resource.UserId;
    }
    
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
            await Container.ReplaceItemAsync(sessionDocument with { Ttl = sessionLengthSeconds}, sessionId, requestOptions:_noResponseContent, cancellationToken: context.Cancellation);
        
        return new UserSession(sessionDocument.UserId, sessionId, sessionDocument.DisplayName, sessionDocument.Handle);
    }
    
    public async Task EndSessionAsync(string sessionId, OperationContext context)
    {
        var sessionKey = SessionDocument.Key(sessionId);
        var response = await Container.DeleteItemAsync<SessionDocument>(sessionKey.Id, new PartitionKey(sessionKey.Pk), requestOptions: _noResponseContent, cancellationToken: context.Cancellation);
        context.AddRequestCharge(response.RequestCharge);
    }

    public async Task DeleteSessionDataAsync(string userId, OperationContext context)
    {
        try
        {
            var passwordLoginKey = PasswordLoginDocument.Key(userId);
            var response = await Container.DeleteItemAsync<PasswordLoginDocument>(passwordLoginKey.Id, new PartitionKey(passwordLoginKey.Pk), _noResponseContent, context.Cancellation);
            context.AddRequestCharge(response.RequestCharge);
        }
        catch (CosmosException e) when (e.StatusCode == HttpStatusCode.NotFound)
        {
        }
    }
}