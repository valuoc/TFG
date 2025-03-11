using System.Net;
using Microsoft.Azure.Cosmos;
using SocialApp.WebApi.Features.Services;
using SocialApp.WebApi.Features.Account.Databases;
using SocialApp.WebApi.Features.Account.Documents;
using SocialApp.WebApi.Features.Account.Exceptions;
using User = SocialApp.WebApi.Features.Session.Services.User;

namespace SocialApp.WebApi.Features.Account.Services;

public class AccountService
{
    private readonly AccountDatabase _database;
    private static readonly ItemRequestOptions _noResponseContent = new(){ EnableContentResponseOnWrite = false};
    
    public AccountService(AccountDatabase database)
    {
        _database = database;
    }
    
    public async ValueTask<string> RegisterAsync(string email, string handle, string displayName, string password, OperationContext context)
    {
        var container = _database.GetContainer();
        var userId = Guid.NewGuid().ToString("N");
        
        var pendingUserAccount = new PendingAccountDocument(Ulid.NewUlid().ToString(), email, userId, handle, DateTime.UtcNow);
        var user = await RegisterAccountInternalAsync(container, displayName, password, pendingUserAccount,context);

        try
        {
            context.SuppressCancellation();
            context.Signal("complete-pending-account");
            await container.DeleteItemAsync<PendingAccountDocument>(pendingUserAccount.Id, new PartitionKey(pendingUserAccount.Pk), _noResponseContent, context.Cancellation);
        }
        catch (CosmosException)
        {
            // Log ?
            context.Signal("clean-up-after-error");
            await PendingAccountCleanUpAsync(container, pendingUserAccount, context.Cancellation);
        }
        return user.Id;
    }

    private static async Task<AccountDocument> RegisterAccountInternalAsync(Container container,  string displayName, string password, PendingAccountDocument pendingUserAccount, OperationContext context)
    {
        var userId = pendingUserAccount.UserId;
        var email = pendingUserAccount.Email;
        var handle = pendingUserAccount.Handle;
        
        try
        {
            context.Signal("pending-account");
            await container.CreateItemAsync(pendingUserAccount, requestOptions: _noResponseContent, cancellationToken: context.Cancellation);
        }
        catch (CosmosException e)
        {
            throw new AccountSocialAppException(AccountError.UnexpectedError, e);
        }

        var emailLock = new AccountEmailDocument(email, userId, Passwords.HashPassword(password)){ Ttl = 5_000};
        try
        {
            context.Signal("email-lock");
            await container.CreateItemAsync(emailLock, requestOptions: _noResponseContent, cancellationToken: context.Cancellation);
        }
        catch (CosmosException e) when ( e.StatusCode == HttpStatusCode.Conflict)
        {
            throw new AccountSocialAppException(AccountError.EmailAlreadyRegistered, e);
        }
        catch (CosmosException e)
        {
            throw new AccountSocialAppException(AccountError.UnexpectedError, e);
        }
        
        var handleLock = new AccountHandleDocument(handle, userId){ Ttl = 5_000};
        try
        {
            context.Signal("handle-lock");
            await container.CreateItemAsync(handleLock,  requestOptions: _noResponseContent, cancellationToken: context.Cancellation);

        }
        catch (CosmosException e) when ( e.StatusCode == HttpStatusCode.Conflict)
        {
            throw new AccountSocialAppException(AccountError.HandleAlreadyRegistered, e);
        }
        catch (CosmosException e)
        {
            throw new AccountSocialAppException(AccountError.UnexpectedError, e);
        }

        var user = new AccountDocument(userId, displayName, email, handle, AccountStatus.Pending);
        try
        {
            context.Signal("user");
            await container.CreateItemAsync(user,  requestOptions: _noResponseContent, cancellationToken: context.Cancellation);
        
            context.Signal("complete-email-lock");
            await container.ReplaceItemAsync(emailLock with { Ttl = -1}, emailLock.Id, new PartitionKey(emailLock.Pk), _noResponseContent, context.Cancellation);
        
            context.Signal("complete-handle-lock");
            await container.ReplaceItemAsync(handleLock with { Ttl = -1}, handleLock.Id, new PartitionKey(handleLock.Pk), _noResponseContent, context.Cancellation);
        
            context.Signal("complete-user");
            await container.ReplaceItemAsync(user with { Status = AccountStatus.Completed}, user.Id, new PartitionKey(user.Pk), _noResponseContent, context.Cancellation);

        }
        catch (CosmosException e)
        {
            throw new AccountSocialAppException(AccountError.UnexpectedError, e);
        }

        return user;
    }

    public async ValueTask<User?> FindAccountAsync(string email, string password, OperationContext context)
    {
        try
        {
            var emailKey = AccountEmailDocument.Key(email);
            var users = _database.GetContainer();
            var emailResponse = await users.ReadItemAsync<AccountEmailDocument>(emailKey.Id, new PartitionKey(emailKey.Pk), cancellationToken: context.Cancellation);
            if (emailResponse.Resource == null || emailResponse.Resource.Password != Passwords.HashPassword(password))
            {
                return null;
            }
        
            var accountKey = AccountDocument.Key(emailResponse.Resource.UserId);
            var accountResponse = await users.ReadItemAsync<AccountDocument>(accountKey.Id, new PartitionKey(accountKey.Pk), cancellationToken: context.Cancellation);
            if (accountResponse.Resource == null || accountResponse.Resource.Status != AccountStatus.Completed)
            {
                return null;
            }

            return new User(accountResponse.Resource.UserId, accountResponse.Resource.DisplayName, accountResponse.Resource.Handle);
        }
        catch (CosmosException e) when (e.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
        catch (CosmosException e)
        {
            throw new AccountSocialAppException(AccountError.UnexpectedError, e);
        }
    }

    public async ValueTask<int> RemovedExpiredPendingAccountsAsync(TimeSpan timeLimit, CancellationToken cancel)
    {
        var container = _database.GetContainer();
        var pendingCount = 0; 
        var expiryLimit = PendingAccountDocument.Key(Ulid.NewUlid(DateTimeOffset.UtcNow.Add(-timeLimit)).ToString());
        var query = new QueryDefinition("select * from u where u.pk = @pk and u.id < @id")
            .WithParameter("@pk", expiryLimit.Pk)
            .WithParameter("@id", expiryLimit.Id);
        
        using var iterator = container.GetItemQueryIterator<PendingAccountDocument>(query);
        while (iterator.HasMoreResults)
        {
            var pendings = await iterator.ReadNextAsync(cancel);
            foreach (var pending in pendings)
            {
                pendingCount++;
                await PendingAccountCleanUpAsync(container, pending, cancel);
            }
        }
        return pendingCount;
    }

    private static async ValueTask PendingAccountCleanUpAsync(Container container, PendingAccountDocument pending, CancellationToken cancel)
    {
        var userStatus = AccountStatus.Pending;
        try
        {
            var userKey = AccountDocument.Key(pending.UserId);
            var user = await container.ReadItemAsync<AccountDocument>(userKey.Id, new PartitionKey(userKey.Pk), cancellationToken: cancel);
            userStatus = user.Resource.Status;
            if (userStatus == AccountStatus.Pending)
            {
                await container.DeleteItemAsync<AccountDocument>(user.Resource.Id, new PartitionKey(user.Resource.Pk), _noResponseContent, cancel);
            }
        }
        catch (Exception) { }

        if (userStatus == AccountStatus.Pending)
        {
            try
            {
                var emailLock = AccountEmailDocument.Key(pending.Email);
                await container.DeleteItemAsync<AccountEmailDocument>(emailLock.Id, new PartitionKey(emailLock.Pk), _noResponseContent, cancel);
            }
            catch (Exception)
            {
            }

            try
            {
                var handleLock = AccountHandleDocument.Key(pending.Handle);
                await container.DeleteItemAsync<AccountHandleDocument>(handleLock.Id, new PartitionKey(handleLock.Pk), _noResponseContent, cancel);
            }
            catch (Exception)
            {
            }
        }

        await container.DeleteItemAsync<PendingAccountDocument>(pending.Id, new PartitionKey(pending.Pk), _noResponseContent, cancel);
    }
}