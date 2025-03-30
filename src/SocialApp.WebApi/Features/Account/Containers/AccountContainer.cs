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
    : base(database)
    { }
    
    public async Task DeletePendingAccountAsync(PendingAccountDocument pendingUserAccount, OperationContext context)
    {
        await Container.DeleteItemAsync<PendingAccountDocument>(pendingUserAccount.Id, new PartitionKey(pendingUserAccount.Pk), _noResponseContent, context.Cancellation);
    }

    public async Task<PendingAccountDocument> RegisterPendingAccountAsync(string userId, string email, string handle, OperationContext context)
    {
        var pendingUserAccount = new PendingAccountDocument(Ulid.NewUlid().ToString(), email, userId, handle, DateTime.UtcNow);
        try
        {
            await Container.CreateItemAsync(pendingUserAccount, requestOptions: _noResponseContent, cancellationToken: context.Cancellation);
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
            var user = await Container.ReadItemAsync<AccountDocument>(userKey.Id, new PartitionKey(userKey.Pk), cancellationToken: context.Cancellation);
            userStatus = user.Resource.Status;
            if (userStatus == AccountStatus.Pending)
            {
                await Container.DeleteItemAsync<AccountDocument>(user.Resource.Id, new PartitionKey(user.Resource.Pk), _noResponseContent, context.Cancellation);
            }
        }
        catch (Exception) { }

        if (userStatus == AccountStatus.Pending)
        {
            isIncomplete = true;
            try
            {
                var emailLock = AccountEmailDocument.Key(pending.Email);
                await Container.DeleteItemAsync<AccountEmailDocument>(emailLock.Id, new PartitionKey(emailLock.Pk), _noResponseContent, context.Cancellation);
            }
            catch (CosmosException e) when (e.StatusCode == HttpStatusCode.NotFound)
            {
            }

            try
            {
                var handleLock = AccountHandleDocument.Key(pending.Handle);
                await Container.DeleteItemAsync<AccountHandleDocument>(handleLock.Id, new PartitionKey(handleLock.Pk), _noResponseContent, context.Cancellation);
            }
            catch (CosmosException e) when (e.StatusCode == HttpStatusCode.NotFound)
            {
            }
        }

        await Container.DeleteItemAsync<PendingAccountDocument>(pending.Id, new PartitionKey(pending.Pk), _noResponseContent, context.Cancellation);
        return isIncomplete;
    }

    public async Task CompleteHandleLock(AccountHandleDocument handleLock, OperationContext context)
    {
        await Container.ReplaceItemAsync(handleLock with { Ttl = -1}, handleLock.Id, new PartitionKey(handleLock.Pk), _noResponseContent, context.Cancellation);
    }

    public async Task CompleteEmailLockAsync(AccountEmailDocument emailLock, OperationContext context)
    {
        await Container.ReplaceItemAsync(emailLock with { Ttl = -1}, emailLock.Id, new PartitionKey(emailLock.Pk), _noResponseContent, context.Cancellation);
    }

    public async Task<AccountDocument> CreateAccountAsPendingAsync(string userId, string email, string handle, OperationContext context)
    {
        var user = new AccountDocument(userId, email, handle, AccountStatus.Pending);
        await Container.CreateItemAsync(user,  requestOptions: _noResponseContent, cancellationToken: context.Cancellation);
        return user;
    }

    public async Task<AccountHandleDocument> RegisterHandleAsync(string userId, string handle, OperationContext context)
    {
        var handleLock = new AccountHandleDocument(handle, userId){ Ttl = 5_000};
        try
        {
            context.Signal("handle-lock");
            await Container.CreateItemAsync(handleLock,  requestOptions: _noResponseContent, cancellationToken: context.Cancellation);

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
            await Container.CreateItemAsync(emailLock, requestOptions: _noResponseContent, cancellationToken: context.Cancellation);
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
        await Container.ReplaceItemAsync(user with { Status = AccountStatus.Completed}, user.Id, new PartitionKey(user.Pk), _noResponseContent, context.Cancellation);
    }
    
    public async IAsyncEnumerable<PendingAccountDocument> GetExpiredPendingAccountsAsync(TimeSpan timeLimit, OperationContext context)
    {
        var expiryLimit = PendingAccountDocument.Key(Ulid.NewUlid(DateTimeOffset.UtcNow.Add(-timeLimit)).ToString());
        var query = new QueryDefinition("select * from c where c.pk = @pk and c.sk < @id")
            .WithParameter("@pk", expiryLimit.Pk)
            .WithParameter("@id", expiryLimit.Id);
        
        using var iterator = Container.GetItemQueryIterator<PendingAccountDocument>(query);
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