using System.Net;
using Microsoft.Azure.Cosmos;
using SocialApp.WebApi.Features.Services;
using SocialApp.WebApi.Features.Account.Databases;
using SocialApp.WebApi.Features.Account.Documents;
using SocialApp.WebApi.Features.Account.Exceptions;
using SocialApp.WebApi.Features.Content.Documents;

namespace SocialApp.WebApi.Features.Account.Services;

public class AccountService
{
    private readonly AccountDatabase _accountDb;
    private readonly ProfileDatabase _profileDb;
    private static readonly ItemRequestOptions _noResponseContent = new(){ EnableContentResponseOnWrite = false};
    private static readonly TransactionalBatchItemRequestOptions _transactionNoResponse = new() { EnableContentResponseOnWrite = false };
    
    public AccountService(AccountDatabase accountDb, ProfileDatabase profileDb)
    {
        _accountDb = accountDb;
        _profileDb = profileDb;
    }
    
    public async ValueTask<string> RegisterAsync(string email, string handle, string displayName, string password, OperationContext context)
    {
        var accounts = _accountDb.GetContainer();
        var userId = Guid.NewGuid().ToString("N");
        
        var pendingUserAccount = new PendingAccountDocument(Ulid.NewUlid().ToString(), email, userId, handle, DateTime.UtcNow);
        var account = await RegisterAccountInternalAsync(accounts, displayName, password, pendingUserAccount,context);

        try
        {
            context.SuppressCancellation();
            context.Signal("complete-pending-account");
            await accounts.DeleteItemAsync<PendingAccountDocument>(pendingUserAccount.Id, new PartitionKey(pendingUserAccount.Pk), _noResponseContent, context.Cancellation);
        }
        catch (CosmosException)
        {
            // Log ?
            context.Signal("clean-up-after-error");
            await PendingAccountCleanUpAsync(accounts, _profileDb.GetContainer(),  pendingUserAccount, context.Cancellation);
        }
        return account.Id;
    }

    private async Task<AccountDocument> RegisterAccountInternalAsync(Container accounts,  string displayName, string password, PendingAccountDocument pendingUserAccount, OperationContext context)
    {
        var userId = pendingUserAccount.UserId;
        var email = pendingUserAccount.Email;
        var handle = pendingUserAccount.Handle;
        
        try
        {
            context.Signal("pending-account");
            await accounts.CreateItemAsync(pendingUserAccount, requestOptions: _noResponseContent, cancellationToken: context.Cancellation);
        }
        catch (CosmosException e)
        {
            throw new AccountException(AccountError.UnexpectedError, e);
        }

        var emailLock = new AccountEmailDocument(email, userId){ Ttl = 5_000};
        try
        {
            context.Signal("email-lock");
            await accounts.CreateItemAsync(emailLock, requestOptions: _noResponseContent, cancellationToken: context.Cancellation);
        }
        catch (CosmosException e) when ( e.StatusCode == HttpStatusCode.Conflict)
        {
            throw new AccountException(AccountError.EmailAlreadyRegistered, e);
        }
        catch (CosmosException e)
        {
            throw new AccountException(AccountError.UnexpectedError, e);
        }
        
        var handleLock = new AccountHandleDocument(handle, userId){ Ttl = 5_000};
        try
        {
            context.Signal("handle-lock");
            await accounts.CreateItemAsync(handleLock,  requestOptions: _noResponseContent, cancellationToken: context.Cancellation);

        }
        catch (CosmosException e) when ( e.StatusCode == HttpStatusCode.Conflict)
        {
            throw new AccountException(AccountError.HandleAlreadyRegistered, e);
        }
        catch (CosmosException e)
        {
            throw new AccountException(AccountError.UnexpectedError, e);
        }

        
        try
        {
            var user = new AccountDocument(userId, email, handle, AccountStatus.Pending);
            context.Signal("user");
            await accounts.CreateItemAsync(user,  requestOptions: _noResponseContent, cancellationToken: context.Cancellation);
        
            context.Signal("complete-email-lock");
            await accounts.ReplaceItemAsync(emailLock with { Ttl = -1}, emailLock.Id, new PartitionKey(emailLock.Pk), _noResponseContent, context.Cancellation);
        
            context.Signal("complete-handle-lock");
            await accounts.ReplaceItemAsync(handleLock with { Ttl = -1}, handleLock.Id, new PartitionKey(handleLock.Pk), _noResponseContent, context.Cancellation);
        
            var profile = new ProfileDocument(userId, displayName, email, handle);
            context.Signal("create-profile");
            var profiles = _profileDb.GetContainer();
            var batch = profiles.CreateTransactionalBatch(new PartitionKey(profile.Pk));
            batch.CreateItem(profile, requestOptions: _transactionNoResponse);
            batch.CreateItem(new PendingCommentsDocument(userId), _transactionNoResponse);
            await batch.ExecuteAsync(context.Cancellation);
            
            context.Signal("complete-user");
            await accounts.ReplaceItemAsync(user with { Status = AccountStatus.Completed}, user.Id, new PartitionKey(user.Pk), _noResponseContent, context.Cancellation);

            // Login is created on successful account.
            // If this step fails, user could use password recovery
            context.Signal("create-login");
            var passwordLogin = new PasswordLoginDocument(userId, email, Passwords.HashPassword(password));
            await profiles.CreateItemAsync(passwordLogin,  requestOptions: _noResponseContent, cancellationToken: context.Cancellation);
            
            return user;
        }
        catch (CosmosException e)
        {
            throw new AccountException(AccountError.UnexpectedError, e);
        }
        
    }

    public async ValueTask<ProfileDocument?> LoginWithPasswordAsync(string email, string password, OperationContext context)
    {
        try
        {
            var loginKey = PasswordLoginDocument.Key(email);
            var profiles = _profileDb.GetContainer();
            var emailResponse = await profiles.ReadItemAsync<PasswordLoginDocument>(loginKey.Id, new PartitionKey(loginKey.Pk), cancellationToken: context.Cancellation);
            if (emailResponse.Resource == null || emailResponse.Resource.Password != Passwords.HashPassword(password))
            {
                return null;
            }
            
            var profileKey = ProfileDocument.Key(emailResponse.Resource.UserId);
            var profileResponse = await profiles.ReadItemAsync<ProfileDocument>(profileKey.Id, new PartitionKey(profileKey.Pk), cancellationToken: context.Cancellation);
            if (profileResponse.Resource == null)
            {
                return null;
            }

            return profileResponse.Resource;
        }
        catch (CosmosException e) when (e.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
        catch (CosmosException e)
        {
            throw new AccountException(AccountError.UnexpectedError, e);
        }
    }

    public async ValueTask<int> RemovedExpiredPendingAccountsAsync(TimeSpan timeLimit, CancellationToken cancel)
    {
        var accounts = _accountDb.GetContainer();
        var profiles = _profileDb.GetContainer();
        var pendingCount = 0; 
        var expiryLimit = PendingAccountDocument.Key(Ulid.NewUlid(DateTimeOffset.UtcNow.Add(-timeLimit)).ToString());
        var query = new QueryDefinition("select * from u where u.pk = @pk and u.id < @id")
            .WithParameter("@pk", expiryLimit.Pk)
            .WithParameter("@id", expiryLimit.Id);
        
        using var iterator = accounts.GetItemQueryIterator<PendingAccountDocument>(query);
        while (iterator.HasMoreResults)
        {
            var pendings = await iterator.ReadNextAsync(cancel);
            foreach (var pending in pendings)
            {
                pendingCount++;
                try
                {
                    await PendingAccountCleanUpAsync(accounts, profiles, pending, cancel);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
            }
        }
        return pendingCount;
    }

    private static async ValueTask PendingAccountCleanUpAsync(Container accounts, Container profiles, PendingAccountDocument pending, CancellationToken cancel)
    {
        var userStatus = AccountStatus.Pending;
        try
        {
            var userKey = AccountDocument.Key(pending.UserId);
            var user = await accounts.ReadItemAsync<AccountDocument>(userKey.Id, new PartitionKey(userKey.Pk), cancellationToken: cancel);
            userStatus = user.Resource.Status;
            if (userStatus == AccountStatus.Pending)
            {
                await accounts.DeleteItemAsync<AccountDocument>(user.Resource.Id, new PartitionKey(user.Resource.Pk), _noResponseContent, cancel);
            }
        }
        catch (Exception) { }

        if (userStatus == AccountStatus.Pending)
        {
            try
            {
                var emailLock = AccountEmailDocument.Key(pending.Email);
                await accounts.DeleteItemAsync<AccountEmailDocument>(emailLock.Id, new PartitionKey(emailLock.Pk), _noResponseContent, cancel);
            }
            catch (CosmosException e) when (e.StatusCode == HttpStatusCode.NotFound)
            {
            }

            try
            {
                var handleLock = AccountHandleDocument.Key(pending.Handle);
                await accounts.DeleteItemAsync<AccountHandleDocument>(handleLock.Id, new PartitionKey(handleLock.Pk), _noResponseContent, cancel);
            }
            catch (CosmosException e) when (e.StatusCode == HttpStatusCode.NotFound)
            {
            }
            
            try
            {
                var profileKey = ProfileDocument.Key(pending.UserId);
                await profiles.DeleteItemAsync<ProfileDocument>(profileKey.Id, new PartitionKey(profileKey.Pk), _noResponseContent, cancel);
            }
            catch (CosmosException e) when (e.StatusCode == HttpStatusCode.NotFound)
            {
            }
            
            try
            {
                var pendingComments = PendingCommentsDocument.Key(pending.UserId);
                await profiles.DeleteItemAsync<PendingCommentsDocument>(pendingComments.Id, new PartitionKey(pendingComments.Pk), _noResponseContent, cancel);
            }
            catch (CosmosException e) when (e.StatusCode == HttpStatusCode.NotFound)
            {
            }
            
            try
            {
                var passwordLoginKey = PasswordLoginDocument.Key(pending.UserId);
                await profiles.DeleteItemAsync<PasswordLoginDocument>(passwordLoginKey.Id, new PartitionKey(passwordLoginKey.Pk), _noResponseContent, cancel);
            }
            catch (CosmosException e) when (e.StatusCode == HttpStatusCode.NotFound)
            {
            }
        }

        await accounts.DeleteItemAsync<PendingAccountDocument>(pending.Id, new PartitionKey(pending.Pk), _noResponseContent, cancel);
    }
}