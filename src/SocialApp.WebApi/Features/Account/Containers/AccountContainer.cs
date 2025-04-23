using System.Net;
using Microsoft.Azure.Cosmos;
using SocialApp.WebApi.Data._Shared;
using SocialApp.WebApi.Data.Account;
using SocialApp.WebApi.Features._Shared.Services;
using SocialApp.WebApi.Features.Account.Exceptions;

namespace SocialApp.WebApi.Features.Account.Containers;

public sealed class AccountContainer : CosmoContainer
{
    private static readonly ItemRequestOptions _noResponseContent = new(){ EnableContentResponseOnWrite = false};
    private static readonly TransactionalBatchItemRequestOptions _transactionNoResponse = new() { EnableContentResponseOnWrite = false };
    
    public AccountContainer(AccountDatabase database)
    : base(database, "accounts")
    { }
    
    public async Task DeletePendingAccountAsync(PendingAccountDocument pendingUserAccount, OperationContext context)
    {
        var response = await Container.DeleteItemAsync<PendingAccountDocument>(pendingUserAccount.Id, new PartitionKey(pendingUserAccount.Pk), _noResponseContent, context.Cancellation);
        context.AddRequestCharge(response.RequestCharge);
    }

    public async Task<PendingAccountDocument> RegisterPendingAccountAsync(string userId, string email, string handle, OperationContext context)
    {
        var pendingUserAccount = new PendingAccountDocument(Ulid.NewUlid().ToString(), email, userId, handle, DateTime.UtcNow);
        try
        {
            var response = await Container.CreateItemAsync(pendingUserAccount, requestOptions: _noResponseContent, cancellationToken: context.Cancellation);
            context.AddRequestCharge(response.RequestCharge);
        }
        catch (CosmosException e)
        {
            throw new AccountException(AccountError.UnexpectedError, e);
        }

        return pendingUserAccount;
    }

    public async Task<bool> TryDeleteAccountLocksAsync(PendingAccountDocument pending, OperationContext context)
    {
        var success = true;
        try
        {
            var emailLock = EmailLockDocument.Key(pending.Email);
            var response = await Container.DeleteItemAsync<EmailLockDocument>(emailLock.Id, new PartitionKey(emailLock.Pk), _noResponseContent, context.Cancellation);
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
            var handleLock = HandleLockDocument.Key(pending.Handle);
            var response = await Container.DeleteItemAsync<HandleLockDocument>(handleLock.Id, new PartitionKey(handleLock.Pk), _noResponseContent, context.Cancellation);
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
    
    public async Task AttemptHandleLockAsync(string userId, string handle, OperationContext context)
    {
        var handleLock = new HandleLockDocument(handle, userId);
        try
        {
            context.Signal("handle-lock");
            var response = await Container.CreateItemAsync(handleLock,  requestOptions: _noResponseContent, cancellationToken: context.Cancellation);
            context.AddRequestCharge(response.RequestCharge);
        }
        catch (CosmosException e) when ( e.StatusCode == HttpStatusCode.Conflict)
        {
            throw new AccountException(AccountError.HandleAlreadyRegistered, e);
        }
        catch (CosmosException e)
        {
            throw new AccountException(AccountError.UnexpectedError, e);
        }
    }

    public async Task AttemptEmailLockAsync(string userId, string email, OperationContext context)
    {
        var emailLock = new EmailLockDocument(email, userId);
        try
        {
            context.Signal("email-lock");
            var response = await Container.CreateItemAsync(emailLock, requestOptions: _noResponseContent, cancellationToken: context.Cancellation);
            context.AddRequestCharge(response.RequestCharge);
        }
        catch (CosmosException e) when ( e.StatusCode == HttpStatusCode.Conflict)
        {
            throw new AccountException(AccountError.EmailAlreadyRegistered, e);
        }
        catch (CosmosException e)
        {
            throw new AccountException(AccountError.UnexpectedError, e);
        }
    }
    
    public async IAsyncEnumerable<PendingAccountDocument> GetExpiredPendingAccountsAsync(TimeSpan timeLimit, OperationContext context)
    {
        var expiryLimit = PendingAccountDocument.Key(Ulid.NewUlid(DateTimeOffset.UtcNow.Add(-timeLimit)).ToString());
        var query = new QueryDefinition("select * from c where c.pk = @pk and c.sk < @id")
            .WithParameter("@pk", expiryLimit.Pk)
            .WithParameter("@id", expiryLimit.Id);
        
        using var iterator = Container.GetItemQueryIterator<PendingAccountDocument>(query, null, new QueryRequestOptions()
        {
            PopulateIndexMetrics = true
        });
        while (iterator.HasMoreResults)
        {
            var pendings = await iterator.ReadNextAsync(context.Cancellation);
            context.AddRequestCharge(pendings.RequestCharge);
            context.SaveDebugMetrics(pendings.IndexMetrics);
            foreach (var pending in pendings)
            {
                yield return pending;
            }
        }
    }
}