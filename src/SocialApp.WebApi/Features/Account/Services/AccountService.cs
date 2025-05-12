using Microsoft.Azure.Cosmos;
using SocialApp.Models.Account;
using SocialApp.WebApi.Data.Account;
using SocialApp.WebApi.Data.User;
using SocialApp.WebApi.Features._Shared.Services;
using SocialApp.WebApi.Features.Account.Containers;
using SocialApp.WebApi.Features.Account.Exceptions;
using SocialApp.WebApi.Features.Account.Queries;

namespace SocialApp.WebApi.Features.Account.Services;

public interface IAccountService
{
    Task<string> RegisterAsync(RegisterRequest request, OperationContext context);
    Task<int> RemovedExpiredPendingAccountsAsync(TimeSpan timeLimit, OperationContext context);
}

public class AccountService : IAccountService
{
    private readonly IQueries _queries;
    private readonly AccountDatabase _accountDb;
    private readonly UserDatabase _userDb;
    private readonly ILogger<AccountService> _logger;

    public AccountService(AccountDatabase accountDb, UserDatabase userDb, ILogger<AccountService> logger, IQueries queries)
    {
        _accountDb = accountDb;
        _userDb = userDb;
        _logger = logger;
        _queries = queries;
    }
    
    private ProfileContainer GetProfileContainer()
        => new(_userDb);
    
    private AccountContainer GetAccountContainer()
        => new(_accountDb);
    
    public async Task<string> RegisterAsync(RegisterRequest request, OperationContext context)
    {
        var accounts = GetAccountContainer();
        
        var userId = request.DisplayName.ToLowerInvariant() +"_"+ Guid.NewGuid().ToString("N");
        var pending = new PendingAccountDocument(Ulid.NewUlid().ToString(), request.Email, userId, request.Handle, DateTime.UtcNow);
        try
        {
            context.Signal("pending-account");
            var uow = accounts.CreateUnitOfWork(pending.Pk);
            uow.Create(pending);
            await uow.SaveChangesAsync(context);

            await RegisterAccountInternalAsync(accounts, request.DisplayName, request.Password, pending, context);

            context.SuppressCancellation();
            context.Signal("complete-pending-account");
        }
        catch (AccountException) { throw; }
        catch (Exception ex)
        {
            throw new AccountException(AccountError.UnexpectedError, ex);
        }
        
        try
        {
            var uow = accounts.CreateUnitOfWork(pending.Pk);
            uow.Delete(pending);
            await uow.SaveChangesAsync(context);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Unable to delete pending account.");
        }

        return userId;
    }
    
    private async Task RegisterAccountInternalAsync(AccountContainer accounts, string displayName, string password, PendingAccountDocument pendingUserAccount, OperationContext context)
    {
        var userId = pendingUserAccount.UserId;
        var email = pendingUserAccount.Email;
        var handle = pendingUserAccount.Handle;

        var emailLock = new EmailLockDocument(email, userId);
        context.Signal("email-lock");
        
        if(!await accounts.TryCreateIfNotExistsAsync(emailLock, context))
            throw new AccountException(AccountError.EmailAlreadyRegistered);
        
        
        var handleLock = new HandleLockDocument(handle, userId);
        context.Signal("handle-lock");
        if(!await accounts.TryCreateIfNotExistsAsync(handleLock, context))
            throw new AccountException(AccountError.HandleAlreadyRegistered);

        try
        {
            var profiles = GetProfileContainer();
            
            context.Signal("create-handle");
            var handleDocument = new HandleDocument(handle, userId);
            var uow = profiles.CreateUnitOfWork(handleDocument.Pk);
            uow.Create(handleDocument);
            await uow.SaveChangesAsync(context);
            
            context.Signal("create-login");
            var passwordLogin = new PasswordLoginDocument(userId, email, Passwords.HashPassword(password));
            uow = profiles.CreateUnitOfWork(passwordLogin.Pk);
            uow.Create(passwordLogin);
            await uow.SaveChangesAsync(context);
            
            context.Signal("create-profile");
            var profile = new ProfileDocument(userId, displayName, email, handle);
            uow = profiles.CreateUnitOfWork(profile.Pk);
            uow.Create(profile);
            await uow.SaveChangesAsync(context);
        }
        catch (CosmosException e)
        {
            throw new AccountException(AccountError.UnexpectedError, e);
        }
    }

    public async Task<int> RemovedExpiredPendingAccountsAsync(TimeSpan timeLimit, OperationContext context)
    {
        var accounts = GetAccountContainer();
        var profiles = GetProfileContainer();
        var pendingCount = 0;

        var query = new ExpiredPendingAccountsQuery(){ Limit = timeLimit };
        await foreach (var pending in _queries.ExecuteQueryManyAsync(accounts, query, context))
        {
            try
            {
                var profile = await profiles.GetProfileAsync(pending.UserId, context);
                if (profile == null)
                {
                    if (await TryDeleteAccountLocksAsync(accounts, pending, context))
                    {
                        if(await DeletePendingDataAsync(profiles, pending, context))
                        {
                            var uow = accounts.CreateUnitOfWork(pending.Pk);
                            uow.Delete(pending);
                            await uow.SaveChangesAsync(context);
                            pendingCount++;
                        }
                    }
                }
                else // The profile document exists, therefore the account was created
                { 
                    var uow = accounts.CreateUnitOfWork(pending.Pk);
                    uow.Delete(pending);
                    await uow.SaveChangesAsync(context);
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Failed to remove pending account.");
            }
        }
        
        return pendingCount;
    }

    private static async Task<bool> DeletePendingDataAsync(ProfileContainer profiles, PendingAccountDocument pending, OperationContext context)
    {
        var success = true;

        try
        {
            var profileKey = ProfileDocument.Key(pending.UserId);
            var uow = profiles.CreateUnitOfWork(profileKey.Pk);
            uow.Delete<ProfileDocument>(profileKey);
            await uow.SaveChangesAsync(context);
        }
        catch (UnitOfWorkException ex) when (ex.Error == OperationError.NotFound)
        {
            
        }
        catch (Exception)
        {
            success = false;
        }
        
        try
        {
            var passwordLoginKey = PasswordLoginDocument.Key(pending.UserId);
            var uow = profiles.CreateUnitOfWork(passwordLoginKey.Pk);
            uow.Delete<PasswordLoginDocument>(passwordLoginKey);
            await uow.SaveChangesAsync(context);
        }
        catch (UnitOfWorkException ex) when (ex.Error == OperationError.NotFound)
        {
            
        }
        catch (Exception)
        {
            success = false;
        }
        
        try
        {
            var handleDocumentKey = HandleLockDocument.Key(pending.UserId);
            var uow = profiles.CreateUnitOfWork(handleDocumentKey.Pk);
            uow.Delete<HandleLockDocument>(handleDocumentKey);
            await uow.SaveChangesAsync(context);
        }
        catch (UnitOfWorkException ex) when (ex.Error == OperationError.NotFound)
        {
            
        }
        catch (Exception)
        {
            success = false;
        }
        return success;
    }

    private static async Task<bool> TryDeleteAccountLocksAsync(AccountContainer accounts, PendingAccountDocument pending, OperationContext context)
    {
        var success = true;
        try
        {
            var emailLock = EmailLockDocument.Key(pending.Email);
            var uow = accounts.CreateUnitOfWork(emailLock.Pk);
            uow.Delete<EmailLockDocument>(emailLock);
            await uow.SaveChangesAsync(context);
        }
        catch (UnitOfWorkException ex) when (ex.Error == OperationError.NotFound)
        {
            
        }
        catch (Exception)
        {
            success = false;
        }

        try
        {
            var handleLock = HandleLockDocument.Key(pending.Handle);
            var uow = accounts.CreateUnitOfWork(handleLock.Pk);
            uow.Delete<HandleLockDocument>(handleLock);
            await uow.SaveChangesAsync(context);
        }
        catch (UnitOfWorkException ex) when (ex.Error == OperationError.NotFound)
        {
            
        }
        catch (Exception)
        {
            success = false;
        }

        return success;
    }
}