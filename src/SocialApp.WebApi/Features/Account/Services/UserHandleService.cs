using SocialApp.WebApi.Data.Account;
using SocialApp.WebApi.Features._Shared.Services;
using SocialApp.WebApi.Features.Account.Containers;
using SocialApp.WebApi.Features.Account.Exceptions;

namespace SocialApp.WebApi.Features.Account.Services;

public interface IUserHandleService
{
    Task<string> GetUserIdAsync(string handle, OperationContext context);
    Task<IReadOnlyList<string?>> GetHandleFromUserIdsAsync(IReadOnlyList<string> userIds, OperationContext context);
}

public class UserHandleService : IUserHandleService
{
    private readonly AccountDatabase _accountDb;

    public UserHandleService(AccountDatabase accountDb)
    {
        _accountDb = accountDb;
    }
    
    private AccountContainer GetAccountContainer()
        => new(_accountDb);

    public async Task<string> GetUserIdAsync(string handle, OperationContext context)
    {
        var accounts = GetAccountContainer();
        var userId = await accounts.GetUserIdFromHandleAsync(handle, context);
        if (string.IsNullOrWhiteSpace(userId))
            throw new AccountException(AccountError.HandleNotFound);
        return userId;
    }

    public async Task<IReadOnlyList<string?>> GetHandleFromUserIdsAsync(IReadOnlyList<string> userIds, OperationContext context)
    {
        if (userIds == null || userIds.Count == 0)
            return userIds;
        
        var accounts = GetAccountContainer();
        return await accounts.GetHandleFromUserIdsAsync(userIds, context);
    }
}