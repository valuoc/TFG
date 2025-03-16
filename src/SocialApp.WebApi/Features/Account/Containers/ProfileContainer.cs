using System.Net;
using Microsoft.Azure.Cosmos;
using SocialApp.WebApi.Features.Account.Documents;
using SocialApp.WebApi.Features.Account.Exceptions;
using SocialApp.WebApi.Features.Content.Documents;
using SocialApp.WebApi.Features.Services;

namespace SocialApp.WebApi.Features.Account.Databases;

public sealed class ProfileContainer
{
    private static readonly ItemRequestOptions _noResponseContent = new(){ EnableContentResponseOnWrite = false};
    private static readonly TransactionalBatchItemRequestOptions _transactionNoResponse = new() { EnableContentResponseOnWrite = false };
    
    private readonly Container _container;

    public ProfileContainer(UserDatabase database)
    {
        _container = database.GetContainer();
    }
    
    public async ValueTask CreateUserProfileAsync(string userId, ProfileDocument profile, OperationContext context)
    {
        var batch = _container.CreateTransactionalBatch(new PartitionKey(profile.Pk));
        batch.CreateItem(profile, requestOptions: _transactionNoResponse);
        batch.CreateItem(new PendingCommentsDocument(userId), _transactionNoResponse);
        var response = await batch.ExecuteAsync(context.Cancellation);
        ThrowErrorIfTransactionFailed(AccountError.UnexpectedError, response);
    }
    
    public async ValueTask CreatePasswordLoginAsync(string userId, string email, string password, OperationContext context)
    {
        var passwordLogin = new PasswordLoginDocument(userId, email, Passwords.HashPassword(password));
        await _container.CreateItemAsync(passwordLogin,  requestOptions: _noResponseContent, cancellationToken: context.Cancellation);
    }
    
    public async ValueTask<ProfileDocument?> FindProfileByUserIdAsync(string userId, OperationContext context)
    {
        var profileKey = ProfileDocument.Key(userId);
        var profileResponse = await _container.ReadItemAsync<ProfileDocument>(profileKey.Id, new PartitionKey(profileKey.Pk), cancellationToken: context.Cancellation);
        if (profileResponse.Resource == null)
        {
            return null;
        }

        return profileResponse.Resource;
    }

    public async ValueTask<string?> FindUserIdByEmailAndPasswordAsync(string email, string password, OperationContext context)
    {
        var loginKey = PasswordLoginDocument.Key(email);
        var emailResponse = await _container.ReadItemAsync<PasswordLoginDocument>(loginKey.Id, new PartitionKey(loginKey.Pk), cancellationToken: context.Cancellation);
        if (emailResponse.Resource == null || emailResponse.Resource.Password != Passwords.HashPassword(password))
        {
            return null;
        }
        return emailResponse.Resource.UserId;
    }
    
    public async Task DeleteProfileDataAsync(string userId, OperationContext context)
    {
        try
        {
            var profileKey = ProfileDocument.Key(userId);
            await _container.DeleteItemAsync<ProfileDocument>(profileKey.Id, new PartitionKey(profileKey.Pk), _noResponseContent, context.Cancellation);
        }
        catch (CosmosException e) when (e.StatusCode == HttpStatusCode.NotFound)
        {
        }
            
        try
        {
            var pendingComments = PendingCommentsDocument.Key(userId);
            await _container.DeleteItemAsync<PendingCommentsDocument>(pendingComments.Id, new PartitionKey(pendingComments.Pk), _noResponseContent, context.Cancellation);
        }
        catch (CosmosException e) when (e.StatusCode == HttpStatusCode.NotFound)
        {
        }
            
        try
        {
            var passwordLoginKey = PasswordLoginDocument.Key(userId);
            await _container.DeleteItemAsync<PasswordLoginDocument>(passwordLoginKey.Id, new PartitionKey(passwordLoginKey.Pk), _noResponseContent, context.Cancellation);
        }
        catch (CosmosException e) when (e.StatusCode == HttpStatusCode.NotFound)
        {
        }
    }
    
    private static void ThrowErrorIfTransactionFailed(AccountError error, TransactionalBatchResponse response)
    {
        if (!response.IsSuccessStatusCode)
        {
            for (var i = 0; i < response.Count; i++)
            {
                var sub = response[i];
                if (sub.StatusCode != HttpStatusCode.FailedDependency)
                    throw new AccountException(error, new CosmosException($"{error}. Batch failed at position [{i}]: {sub.StatusCode}. {response.ErrorMessage}", sub.StatusCode, 0, i.ToString(), 0));
            }
        }
    }
}