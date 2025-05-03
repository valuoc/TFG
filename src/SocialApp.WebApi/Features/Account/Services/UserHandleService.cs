using SocialApp.WebApi.Data._Shared;
using SocialApp.WebApi.Data.Account;
using SocialApp.WebApi.Data.User;
using SocialApp.WebApi.Features._Shared.Services;
using SocialApp.WebApi.Features.Account.Containers;
using SocialApp.WebApi.Features.Account.Exceptions;

namespace SocialApp.WebApi.Features.Account.Services;

public interface IUserHandleService
{
    Task<string> GetUserIdAsync(string handle, OperationContext context);
    Task<IReadOnlyList<string?>> GetHandlesAsync(IReadOnlyList<string> userIds, OperationContext context);
    Task<string?> GetHandleAsync(string userId, OperationContext context);
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
        var key = HandleLockDocument.Key(handle);
        var response = await profiles.GetAsync<HandleLockDocument>(new DocumentKey(key.Pk, key.Id), context);
        if (string.IsNullOrWhiteSpace(response?.UserId))
            throw new AccountException(AccountError.HandleNotFound);
        return response.UserId;
    }

    public async Task<IReadOnlyList<string?>> GetHandlesAsync(IReadOnlyList<string> userIds, OperationContext context)
    {
        if (userIds == null)
            return [];
        
        var keys = userIds.Select(ProfileDocument.Key).ToArray();

        if (keys.Length == 0)
            return [];
        
        var profiles = GetProfileContainer();
        var dic = await profiles.GetManyAsync<ProfileDocument>(keys, context);
        var result = new string?[userIds.Count];
        for (var i = 0; i < result.Length; i++)
            result[i] = dic.TryGetValue(keys[i], out var value) ? value?.Handle : null;
        return result;
    }

    public async Task<string?> GetHandleAsync(string userId, OperationContext context)
    {
        var profiles = GetProfileContainer();
        var key = ProfileDocument.Key(userId);
        var profile = await profiles.GetAsync<ProfileDocument>(key, context);
        return profile?.Handle;
    }
}