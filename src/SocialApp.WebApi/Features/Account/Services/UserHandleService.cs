using SocialApp.WebApi.Data.User;
using SocialApp.WebApi.Features._Shared.Services;
using SocialApp.WebApi.Features.Account.Containers;
using SocialApp.WebApi.Features.Account.Exceptions;

namespace SocialApp.WebApi.Features.Account.Services;

public interface IUserHandleService
{
    Task<string> GetUserIdAsync(string handle, OperationContext context);
    Task<IReadOnlyList<string>> GetHandleFromUserIdsAsync(IReadOnlyList<string> userIds, OperationContext context);
    Task<string> GetHandleFromUserIdAsync(string userId, OperationContext context);
}

public class UserHandleService : IUserHandleService
{
    private readonly UserDatabase _userDb;

    public UserHandleService(UserDatabase userDb)
    {
        _userDb = userDb;
    }
    
    private ProfileContainer GetProfileContainer()
        => new(_userDb);

    public async Task<string> GetUserIdAsync(string handle, OperationContext context)
    {
        var profiles = GetProfileContainer();
        var userId = await profiles.GetUserIdFromHandleAsync(handle, context);
        if (string.IsNullOrWhiteSpace(userId))
            throw new AccountException(AccountError.HandleNotFound);
        return userId;
    }

    public async Task<IReadOnlyList<string>> GetHandleFromUserIdsAsync(IReadOnlyList<string> userIds, OperationContext context)
    {
        if (userIds == null || userIds.Count == 0)
            return userIds;
        
        var profiles = GetProfileContainer();
        return await profiles.GetHandleFromUserIdsAsync(userIds, context);
    }

    public async Task<string> GetHandleFromUserIdAsync(string userId, OperationContext context)
    {
        var profiles = GetProfileContainer();
        return await profiles.GetHandleFromUserIdAsync(userId, context);
    }
}