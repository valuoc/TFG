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
        
        context.Signal("pending-account");
        var pending = await accounts.RegisterPendingAccountAsync(userId, request.Email, request.Handle, context);

        await RegisterAccountInternalAsync(accounts, request.DisplayName, request.Password, pending, context);

        context.SuppressCancellation();
        context.Signal("complete-pending-account");
        
        try
        {
            await accounts.DeletePendingAccountAsync(pending, context);
        }
        catch (CosmosException ex)
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

        await accounts.AttemptEmailLockAsync(userId, email, context);
        await accounts.AttemptHandleLockAsync(userId, handle, context);

        try
        {
            var profiles = GetProfileContainer();
            
            context.Signal("create-handle");
            var handleDocument = new HandleDocument(handle, userId);
            await profiles.RegisterHandleAsync(handleDocument, context);
            
            context.Signal("create-login");
            await profiles.CreatePasswordLoginAsync(userId, email, password, context);
            
            context.Signal("create-profile");
            var profile = new ProfileDocument(userId, displayName, email, handle);
            await profiles.CreateUserProfileAsync(userId, profile, context);
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
                    if (await accounts.TryDeleteAccountLocksAsync(pending, context))
                    {
                        if(await profiles.DeletePendingDataAsync(pending, context))
                        {
                            await accounts.DeletePendingAccountAsync(pending, context);
                            pendingCount++;
                        }
                    }
                }
                else // The profile document exists, therefore the account was created
                { 
                    await accounts.DeletePendingAccountAsync(pending, context);
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Failed to remove pending account.");
            }
        }
        
        return pendingCount;
    }

}