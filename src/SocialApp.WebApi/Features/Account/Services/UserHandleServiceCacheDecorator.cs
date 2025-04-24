using Microsoft.Extensions.Caching.Memory;
using SocialApp.WebApi.Features._Shared.Services;

namespace SocialApp.WebApi.Features.Account.Services;

public sealed class UserHandleServiceCacheDecorator : IUserHandleService
{
    private readonly IUserHandleService _inner;
    private readonly IMemoryCache _cache;

    public UserHandleServiceCacheDecorator(IUserHandleService inner, IMemoryCache cache)
    {
        _inner = inner;
        _cache = cache;
    }
    
    private static string HandleKey(string handle)
        => $"handle>{handle}";
    
    private static string UserIdKey(string userId)
        => $"userid>{userId}";

    public async Task<string> GetUserIdAsync(string handle, OperationContext context)
    {
        var key = HandleKey(handle);
        if(_cache.TryGetValue(key, out string userId) && userId != null)
            return userId;
        
        userId = await _inner.GetUserIdAsync(handle, context);
        
        _cache.Set(key, userId, new MemoryCacheEntryOptions()
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1)
        });

        return userId;
    }

    public async Task<string> GetHandleFromUserIdAsync(string userId, OperationContext context)
    {
        var key = UserIdKey(userId);
        if(_cache.TryGetValue(key, out string handle) && handle != null)
            return handle;
        
        handle = await _inner.GetHandleFromUserIdAsync(userId, context);
        
        _cache.Set(key, handle, new MemoryCacheEntryOptions()
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1)
        });
        
        return handle;
    }
    

    public async Task<IReadOnlyList<string>> GetHandleFromUserIdsAsync(IReadOnlyList<string> userIds, OperationContext context)
    {
        List<string> missing = null;
        List<int> missinIndexes = null;
        List<string> result = new List<string>(userIds.Count);
        for (var i = 0; i < userIds.Count; i++)
        {
            var key = UserIdKey(userIds[i]);
            if (!_cache.TryGetValue(key, out string userId))
            {
                userId = null;
                missing ??= new List<string>();
                missing.Add(userIds[i]);
                missinIndexes ??= new List<int>();
                missinIndexes.Add(i);
            }

            result.Add(userId);
        }

        if (missing != null)
        {
            var missingUsers = await _inner.GetHandleFromUserIdsAsync(missing, context);
            for (var i = 0; i < missing.Count; i++)
            {
                var index = missinIndexes[i];
                var missingResult = missingUsers[i];
                result[index] = missingResult;
                if(!string.IsNullOrWhiteSpace(missingResult))
                {
                    _cache.Set(UserIdKey(userIds[i]), missingResult, new MemoryCacheEntryOptions()
                    {
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1)
                    });
                }
            }
        }
        return result;
    }
}