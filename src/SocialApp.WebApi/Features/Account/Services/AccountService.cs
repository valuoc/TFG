using Microsoft.Azure.Cosmos;
using SocialApp.Models.Account;
using SocialApp.WebApi.Data.Account;
using SocialApp.WebApi.Data.User;
using SocialApp.WebApi.Features._Shared.Services;
using SocialApp.WebApi.Features.Account.Containers;
using SocialApp.WebApi.Features.Account.Exceptions;

namespace SocialApp.WebApi.Features.Account.Services;

public interface IAccountService
{
    Task<string> RegisterAsync(RegisterRequest request, OperationContext context);
}

public class AccountService : IAccountService
{
    private readonly AccountDatabase _accountDb;
    private readonly UserDatabase _userDb;

    public AccountService(AccountDatabase accountDb, UserDatabase userDb)
    {
        _accountDb = accountDb;
        _userDb = userDb;
    }
    
    private ProfileContainer GetProfileContainer()
        => new(_userDb);
    
    private AccountContainer GetAccountContainer()
        => new(_accountDb);
    
    public async Task<string> RegisterAsync(RegisterRequest request, OperationContext context)
    {
        var accounts = GetAccountContainer();
        
        var userId = request.DisplayName.ToLowerInvariant() +"_"+ Guid.NewGuid().ToString("N");
        
        context.Signal("pending-account");
        var pendingUserAccount = await accounts.RegisterPendingAccountAsync(userId, request.Email, request.Handle, context);

        await RegisterAccountInternalAsync(accounts, request.DisplayName, request.Password, pendingUserAccount, context);

        context.SuppressCancellation();
        context.Signal("complete-pending-account");
        
        try
        {
            await accounts.DeletePendingAccountAsync(pendingUserAccount, context);
        }
        catch (CosmosException)
        {
            // Log ?
            context.Signal("clean-up-after-error");
            if(await accounts.PendingAccountCleanUpAsync(pendingUserAccount, context))
            {
                await GetProfileContainer().DeleteProfileDataAsync(pendingUserAccount.UserId, context);
                await accounts.DeleteSessionDataAsync(pendingUserAccount.UserId, context);
            }
        }

        return userId;
    }
    
    private async Task  RegisterAccountInternalAsync(AccountContainer accounts, string displayName, string password, PendingAccountDocument pendingUserAccount, OperationContext context)
    {
        var userId = pendingUserAccount.UserId;
        var email = pendingUserAccount.Email;
        var handle = pendingUserAccount.Handle;

        var emailLock = await accounts.RegisterEmailAsync(userId, email, context);
        var handleLock = await accounts.RegisterHandleAsync(userId, handle, context);

        try
        {
            context.Signal("user");
            var user = await accounts.CreateAccountAsPendingAsync(userId, email, handle, context);

            context.Signal("complete-email-lock");
            await accounts.CompleteEmailLockAsync(emailLock, context);
        
            context.Signal("complete-handle-lock");
            await accounts.CompleteHandleLock(handleLock, context);
            
            context.Signal("create-profile");
            var profiles = GetProfileContainer();
            var profile = new ProfileDocument(userId, displayName, email, handle);
            await profiles.CreateUserProfileAsync(userId, profile, context);

            context.Signal("complete-user");
            await accounts.CompleteAccountAsync(accounts, user, context);

            // Login is created on successful account.
            // If this step fails, user could use password recovery
            context.Signal("create-login");
            await accounts.CreatePasswordLoginAsync(userId, email, password, context);
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
            pendingCount++;
            try
            {
                await accounts.PendingAccountCleanUpAsync(pending, context);
                await profiles.DeleteProfileDataAsync(pending.UserId, context);
                await accounts.DeleteSessionDataAsync(pending.UserId, context);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }
        
        return pendingCount;
    }

}