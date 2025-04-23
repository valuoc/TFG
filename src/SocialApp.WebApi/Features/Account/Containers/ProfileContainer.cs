using System.Net;
using Microsoft.Azure.Cosmos;
using SocialApp.WebApi.Data._Shared;
using SocialApp.WebApi.Data.Account;
using SocialApp.WebApi.Data.User;
using SocialApp.WebApi.Features._Shared.Services;
using SocialApp.WebApi.Features.Account.Exceptions;

namespace SocialApp.WebApi.Features.Account.Containers;

public sealed class ProfileContainer : CosmoContainer
{
    private static readonly ItemRequestOptions _noResponseContent = new(){ EnableContentResponseOnWrite = false};
    private static readonly TransactionalBatchItemRequestOptions _transactionNoResponse = new() { EnableContentResponseOnWrite = false };
    
    public ProfileContainer(UserDatabase database)
        :base(database, "profiles")
    { }
    
    public async Task CreateUserProfileAsync(string userId, ProfileDocument profile, OperationContext context)
    {
        var batch = Container.CreateTransactionalBatch(new PartitionKey(profile.Pk));
        batch.CreateItem(profile, requestOptions: _transactionNoResponse);
        var response = await batch.ExecuteAsync(context.Cancellation);
        context.AddRequestCharge(response.RequestCharge);
        ThrowErrorIfTransactionFailed(AccountError.UnexpectedError, response);
    }
    
    public async Task<ProfileDocument?> GetProfileAsync(string userId, OperationContext context)
    {
        var profileKey = ProfileDocument.Key(userId);
        
        const string sql = "select * from c where c.pk = @pk and c.type = @a";
        var query = new QueryDefinition(sql)
            .WithParameter("@pk", profileKey.Pk)
            .WithParameter("@a", nameof(ProfileDocument));

        ProfileDocument? profile = null;
        await foreach (var document in ExecuteQueryReaderAsync(query, profileKey.Pk, context))
        {
            if(document is ProfileDocument p)
                profile = p;
            else
                throw new InvalidOperationException("Unexpected document type: " + document.GetType().Name);
        }

        return profile;
    }
    
    public async Task<IReadOnlyList<string>> GetHandleFromUserIdsAsync(IReadOnlyList<string> userIds, OperationContext context)
    {
        try
        {
            var keys = userIds.Select(x => ProfileDocument.Key(x));
            var requests = keys.Select(k => (k.Id, new PartitionKey(k.Pk))).ToList();
            var response = await Container.ReadManyItemsAsync<ProfileDocument>(requests, cancellationToken: context.Cancellation);
            context.AddRequestCharge(response.RequestCharge);

            var dic = response.Resource.ToDictionary(x => x.UserId, x => x.Handle);
            var results = new List<string>(userIds.Count);
            for (var i = 0; i < userIds.Count; i++)
            {
                var handle = dic.GetValueOrDefault(userIds[i]);
                results.Add(handle ?? "???");
            }

            return results;
        }
        catch (CosmosException e)
        {
            context.AddRequestCharge(e.RequestCharge);
            throw;
        }
    }
    
    public async Task<string?> GetHandleFromUserIdAsync(string userId, OperationContext context)
    {
        var response = await GetHandleFromUserIdsAsync([userId], context);
        return response?.FirstOrDefault() ?? "???";
    }
    
    public async Task RegisterHandleAsync(HandleDocument handle, OperationContext context)
    {
        var response = await Container.CreateItemAsync(handle,  requestOptions: _noResponseContent, cancellationToken: context.Cancellation);
        context.AddRequestCharge(response.RequestCharge);
    }
    
    public async Task<string?> GetUserIdFromHandleAsync(string handle, OperationContext context)
    {
        try
        {
            var key = HandleLockDocument.Key(handle);
            var response = await Container.ReadItemAsync<HandleLockDocument>(key.Id, new PartitionKey(key.Pk), cancellationToken: context.Cancellation);
            context.AddRequestCharge(response.RequestCharge);
            return response.Resource?.UserId;
        }
        catch (CosmosException e) when (e.StatusCode == HttpStatusCode.NotFound)
        {
            context.AddRequestCharge(e.RequestCharge);
            return null;
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
    
    public async Task<bool> DeletePendingDataAsync(PendingAccountDocument pending, OperationContext context)
    {
        var success = true;
        
        try
        {
            var profileKey = ProfileDocument.Key(pending.UserId);
            var response = await Container.DeleteItemAsync<ProfileDocument>(profileKey.Id, new PartitionKey(profileKey.Pk), _noResponseContent, context.Cancellation);
            context.AddRequestCharge(response.RequestCharge);
        }
        catch (CosmosException e) when (e.StatusCode == HttpStatusCode.NotFound)
        {

        }
        catch (Exception e)
        {
            success = false;
        }
        
        try
        {
            var passwordLoginKey = PasswordLoginDocument.Key(pending.UserId);
            var response = await Container.DeleteItemAsync<PasswordLoginDocument>(passwordLoginKey.Id, new PartitionKey(passwordLoginKey.Pk), _noResponseContent, context.Cancellation);
            context.AddRequestCharge(response.RequestCharge);
        }
        catch (CosmosException e) when (e.StatusCode == HttpStatusCode.NotFound)
        {

        }
        catch (Exception e)
        {
            success = false;
        }
        
        
        try
        {
            var passwordLoginKey = HandleLockDocument.Key(pending.UserId);
            var response = await Container.DeleteItemAsync<HandleLockDocument>(passwordLoginKey.Id, new PartitionKey(passwordLoginKey.Pk), _noResponseContent, context.Cancellation);
            context.AddRequestCharge(response.RequestCharge);
        }
        catch (CosmosException e) when (e.StatusCode == HttpStatusCode.NotFound)
        {

        }
        catch (Exception e)
        {
            success = false;
        }
        return success;
    }
}