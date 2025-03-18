using System.Net;
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
        :base(database)
    { }
    
    public async ValueTask CreatePasswordLoginAsync(string userId, string email, string password, OperationContext context)
    {
        var passwordLogin = new PasswordLoginDocument(userId, email, Passwords.HashPassword(password));
        await Container.CreateItemAsync(passwordLogin,  requestOptions: _noResponseContent, cancellationToken: context.Cancellation);
    }

    public async ValueTask<string?> FindPasswordLoginAsync(string email, string password, OperationContext context)
    {
        var loginKey = PasswordLoginDocument.Key(email);
        var emailResponse = await Container.ReadItemAsync<PasswordLoginDocument>(loginKey.Id, new PartitionKey(loginKey.Pk), cancellationToken: context.Cancellation);
        if (emailResponse.Resource == null || emailResponse.Resource.Password != Passwords.HashPassword(password))
        {
            return null;
        }
        return emailResponse.Resource.UserId;
    }
    
    public async Task CreateSessionAsync(SessionDocument session, OperationContext context)
    {
        await Container.CreateItemAsync(session, requestOptions: _noResponseContent, cancellationToken: context.Cancellation);
    }
    
    public async Task<UserSession?> GetSessionAsync(string sessionId, int sessionLengthSeconds, OperationContext context)
    {
        var sessionKey = SessionDocument.Key(sessionId);
        var response = await Container.ReadItemAsync<SessionDocument>(sessionKey.Id, new PartitionKey(sessionKey.Pk), cancellationToken: context.Cancellation);
        if (response?.Resource == null)
            return null;
        
        if(response.Resource.Ttl < sessionLengthSeconds / 4) // TODO: Patch ?
            await Container.ReplaceItemAsync(response.Resource with { Ttl = sessionLengthSeconds}, sessionId, requestOptions:_noResponseContent, cancellationToken: context.Cancellation);
        
        return new UserSession(response.Resource.UserId, sessionId, response.Resource.DisplayName, response.Resource.Handle);
    }
    
    public async Task EndSessionAsync(string sessionId, OperationContext context)
    {
        var sessionKey = SessionDocument.Key(sessionId);
        await Container.DeleteItemAsync<SessionDocument>(sessionKey.Id, new PartitionKey(sessionKey.Pk), requestOptions: _noResponseContent, cancellationToken: context.Cancellation);
    }

    public async Task DeleteSessionDataAsync(string userId, OperationContext context)
    {
        try
        {
            var passwordLoginKey = PasswordLoginDocument.Key(userId);
            await Container.DeleteItemAsync<PasswordLoginDocument>(passwordLoginKey.Id, new PartitionKey(passwordLoginKey.Pk), _noResponseContent, context.Cancellation);
        }
        catch (CosmosException e) when (e.StatusCode == HttpStatusCode.NotFound)
        {
        }
    }
}