using System.Net;
using Microsoft.Azure.Cosmos;
using SocialApp.WebApi.Features.Account.Documents;
using SocialApp.WebApi.Features.Account.Exceptions;
using SocialApp.WebApi.Features.Services;

namespace SocialApp.WebApi.Features.Account.Databases;

public sealed class AccountContainer
{
    private static readonly ItemRequestOptions _noResponseContent = new(){ EnableContentResponseOnWrite = false};
    private static readonly TransactionalBatchItemRequestOptions _transactionNoResponse = new() { EnableContentResponseOnWrite = false };
    
    private readonly Container _container;
    public AccountContainer(AccountDatabase database)
    {
        _container = database.GetContainer();
    }
    
    public async Task DeletePendingAccountAsync(PendingAccountDocument pendingUserAccount, OperationContext context)
    {
        await _container.DeleteItemAsync<PendingAccountDocument>(pendingUserAccount.Id, new PartitionKey(pendingUserAccount.Pk), _noResponseContent, context.Cancellation);
    }

    public async Task<PendingAccountDocument> RegisterPendingAccountAsync(string userId, string email, string handle, OperationContext context)
    {
        var pendingUserAccount = new PendingAccountDocument(Ulid.NewUlid().ToString(), email, userId, handle, DateTime.UtcNow);
        try
        {
            await _container.CreateItemAsync(pendingUserAccount, requestOptions: _noResponseContent, cancellationToken: context.Cancellation);
        }
        catch (CosmosException e)
        {
            throw new AccountException(AccountError.UnexpectedError, e);
        }

        return pendingUserAccount;
    }
    
    public async ValueTask<bool> PendingAccountCleanUpAsync(PendingAccountDocument pending, OperationContext context)
    {
        var isIncomplete = false;
        var userStatus = AccountStatus.Pending;
        try
        {
            var userKey = AccountDocument.Key(pending.UserId);
            var user = await _container.ReadItemAsync<AccountDocument>(userKey.Id, new PartitionKey(userKey.Pk), cancellationToken: context.Cancellation);
            userStatus = user.Resource.Status;
            if (userStatus == AccountStatus.Pending)
            {
                await _container.DeleteItemAsync<AccountDocument>(user.Resource.Id, new PartitionKey(user.Resource.Pk), _noResponseContent, context.Cancellation);
            }
        }
        catch (Exception) { }

        if (userStatus == AccountStatus.Pending)
        {
            isIncomplete = true;
            try
            {
                var emailLock = AccountEmailDocument.Key(pending.Email);
                await _container.DeleteItemAsync<AccountEmailDocument>(emailLock.Id, new PartitionKey(emailLock.Pk), _noResponseContent, context.Cancellation);
            }
            catch (CosmosException e) when (e.StatusCode == HttpStatusCode.NotFound)
            {
            }

            try
            {
                var handleLock = AccountHandleDocument.Key(pending.Handle);
                await _container.DeleteItemAsync<AccountHandleDocument>(handleLock.Id, new PartitionKey(handleLock.Pk), _noResponseContent, context.Cancellation);
            }
            catch (CosmosException e) when (e.StatusCode == HttpStatusCode.NotFound)
            {
            }
        }

        await _container.DeleteItemAsync<PendingAccountDocument>(pending.Id, new PartitionKey(pending.Pk), _noResponseContent, context.Cancellation);
        return isIncomplete;
    }

    public async Task CompleteHandleLock(AccountHandleDocument handleLock, OperationContext context)
    {
        await _container.ReplaceItemAsync(handleLock with { Ttl = -1}, handleLock.Id, new PartitionKey(handleLock.Pk), _noResponseContent, context.Cancellation);
    }

    public async Task CompleteEmailLockAsync(AccountEmailDocument emailLock, OperationContext context)
    {
        await _container.ReplaceItemAsync(emailLock with { Ttl = -1}, emailLock.Id, new PartitionKey(emailLock.Pk), _noResponseContent, context.Cancellation);
    }

    public async Task<AccountDocument> CreateAccountAsPendingAsync(string userId, string email, string handle, OperationContext context)
    {
        var user = new AccountDocument(userId, email, handle, AccountStatus.Pending);
        await _container.CreateItemAsync(user,  requestOptions: _noResponseContent, cancellationToken: context.Cancellation);
        return user;
    }

    public async Task<AccountHandleDocument> RegisterHandleAsync(string userId, string handle, OperationContext context)
    {
        var handleLock = new AccountHandleDocument(handle, userId){ Ttl = 5_000};
        try
        {
            context.Signal("handle-lock");
            await _container.CreateItemAsync(handleLock,  requestOptions: _noResponseContent, cancellationToken: context.Cancellation);

        }
        catch (CosmosException e) when ( e.StatusCode == HttpStatusCode.Conflict)
        {
            throw new AccountException(AccountError.HandleAlreadyRegistered, e);
        }
        catch (CosmosException e)
        {
            throw new AccountException(AccountError.UnexpectedError, e);
        }

        return handleLock;
    }

    public async Task<AccountEmailDocument> RegisterEmailAsync(string userId, string email, OperationContext context)
    {
        var emailLock = new AccountEmailDocument(email, userId){ Ttl = 5_000};
        try
        {
            context.Signal("email-lock");
            await _container.CreateItemAsync(emailLock, requestOptions: _noResponseContent, cancellationToken: context.Cancellation);
        }
        catch (CosmosException e) when ( e.StatusCode == HttpStatusCode.Conflict)
        {
            throw new AccountException(AccountError.EmailAlreadyRegistered, e);
        }
        catch (CosmosException e)
        {
            throw new AccountException(AccountError.UnexpectedError, e);
        }

        return emailLock;
    }
    
    public async Task CompleteAccountAsync(AccountContainer accounts, AccountDocument user, OperationContext context)
    {
        await _container.ReplaceItemAsync(user with { Status = AccountStatus.Completed}, user.Id, new PartitionKey(user.Pk), _noResponseContent, context.Cancellation);
    }
    
    public async IAsyncEnumerable<PendingAccountDocument> GetExpiredPendingAccountsAsync(TimeSpan timeLimit, OperationContext context)
    {
        var expiryLimit = PendingAccountDocument.Key(Ulid.NewUlid(DateTimeOffset.UtcNow.Add(-timeLimit)).ToString());
        var query = new QueryDefinition("select * from u where u.pk = @pk and u.id < @id")
            .WithParameter("@pk", expiryLimit.Pk)
            .WithParameter("@id", expiryLimit.Id);
        
        using var iterator = _container.GetItemQueryIterator<PendingAccountDocument>(query);
        while (iterator.HasMoreResults)
        {
            var pendings = await iterator.ReadNextAsync(context.Cancellation);
            foreach (var pending in pendings)
            {
                yield return pending;
            }
        }
    }
}