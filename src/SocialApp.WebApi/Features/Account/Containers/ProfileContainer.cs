using System.Net;
using Microsoft.Azure.Cosmos;
using SocialApp.WebApi.Data.User;
using SocialApp.WebApi.Features._Shared.Services;
using SocialApp.WebApi.Features.Account.Exceptions;

namespace SocialApp.WebApi.Features.Account.Containers;

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
        batch.CreateItem(new PendingOperationsDocument(userId), _transactionNoResponse);
        var response = await batch.ExecuteAsync(context.Cancellation);
        ThrowErrorIfTransactionFailed(AccountError.UnexpectedError, response);
    }
    
    public async ValueTask<ProfileDocument?> GetProfileAsync(string userId, OperationContext context)
    {
        var profileKey = ProfileDocument.Key(userId);
        var profileResponse = await _container.ReadItemAsync<ProfileDocument>(profileKey.Id, new PartitionKey(profileKey.Pk), cancellationToken: context.Cancellation);
        if (profileResponse.Resource == null)
        {
            return null;
        }

        return profileResponse.Resource;
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
            var pendingComments = PendingOperationsDocument.Key(userId);
            await _container.DeleteItemAsync<PendingOperationsDocument>(pendingComments.Id, new PartitionKey(pendingComments.Pk), _noResponseContent, context.Cancellation);
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