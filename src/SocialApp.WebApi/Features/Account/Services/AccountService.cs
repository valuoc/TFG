using Microsoft.Azure.Cosmos;
using SocialApp.Models.Account;
using SocialApp.WebApi.Data._Shared;
using SocialApp.WebApi.Data.Account;
using SocialApp.WebApi.Data.User;
using SocialApp.WebApi.Features._Shared.Services;
using SocialApp.WebApi.Features.Account.Containers;
using SocialApp.WebApi.Features.Account.Exceptions;

namespace SocialApp.WebApi.Features.Account.Services;

public interface IAccountService
{
    Task<string> RegisterAsync(RegisterRequest request, OperationContext context);
    Task<int> RemovedExpiredPendingAccountsAsync(TimeSpan timeLimit, OperationContext context);
}

public class AccountService : IAccountService
{
    private readonly AccountDatabase _accountDb;
    private readonly UserDatabase _userDb;
    private readonly ILogger<AccountService> _logger;

    public AccountService(AccountDatabase accountDb, UserDatabase userDb, ILogger<AccountService> logger)
    {
        _accountDb = accountDb;
        _userDb = userDb;
        _logger = logger;
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
            await accounts.CreateAsync(pending, context);

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
            await accounts.DeleteAsync<PendingAccountDocument>(new DocumentKey(pending.Pk, pending.Id), context);
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
            await profiles.CreateAsync(handleDocument, context);
            
            context.Signal("create-login");
            var passwordLogin = new PasswordLoginDocument(userId, email, Passwords.HashPassword(password));
            await profiles.CreateAsync(passwordLogin, context);
            
            context.Signal("create-profile");
            var profile = new ProfileDocument(userId, displayName, email, handle);
            await profiles.CreateAsync(profile, context);
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

        await foreach (var pending in accounts.GetExpiredPendingAccountsAsync(timeLimit, context))
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
                            await accounts.DeleteAsync<PendingAccountDocument>(new DocumentKey(pending.Pk, pending.Id), context);
                            pendingCount++;
                        }
                    }
                }
                else // The profile document exists, therefore the account was created
                { 
                    await accounts.DeleteAsync<PendingAccountDocument>(new DocumentKey(pending.Pk, pending.Id), context);
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
            await profiles.DeleteAsync<ProfileDocument>(new DocumentKey(profileKey.Pk, profileKey.Id), context);
        }
        catch (Exception)
        {
            success = false;
        }
        
        try
        {
            var passwordLoginKey = PasswordLoginDocument.Key(pending.UserId);
            await profiles.DeleteAsync<ProfileDocument>(new DocumentKey(passwordLoginKey.Pk, passwordLoginKey.Id), context);
        }
        catch (Exception)
        {
            success = false;
        }
        
        
        try
        {
            var handleDocument = HandleLockDocument.Key(pending.UserId);
            await profiles.DeleteAsync<HandleLockDocument>(new DocumentKey(handleDocument.Pk, handleDocument.Id), context);
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
            await accounts.DeleteAsync<EmailLockDocument>(new DocumentKey(emailLock.Pk, emailLock.Id), context);
        }
        catch (Exception)
        {
            success = false;
        }

        try
        {
            var handleLock = HandleLockDocument.Key(pending.Handle);
            await accounts.DeleteAsync<HandleLockDocument>(new DocumentKey(handleLock.Pk, handleLock.Id), context);
        }
        catch (Exception)
        {
            success = false;
        }

        return success;
    }
}