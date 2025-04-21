using System.Collections.Concurrent;
using SocialApp.WebApi.Features._Shared.Services;

namespace SocialApp.WebApi.Features.Account.Services;

public sealed class UserHandleServiceCacheDecorator : IUserHandleService
{
    private ConcurrentDictionary<string, string> _handleCache = new();
    private ConcurrentDictionary<string, string> _userIdCache = new();
    
    private readonly IUserHandleService _inner;
    public UserHandleServiceCacheDecorator(IUserHandleService inner)
        => _inner = inner;

    public async Task<string> GetUserIdAsync(string handle, OperationContext context)
    {
        if(_userIdCache.TryGetValue(handle, out string userId))
            return userId;
        
        userId = await _inner.GetUserIdAsync(handle, context);
        _userIdCache.TryAdd(handle, userId);
        return userId;
    }
    
    public async Task<string> GetHandleFromUserIdAsync(string userId, OperationContext context)
    {
        if(!_handleCache.TryGetValue(userId, out string handle))
        {
            handle = await _inner.GetHandleFromUserIdAsync(userId, context);
            _handleCache.TryAdd(userId, handle);
        }
        return handle;
    }

    public async Task<IReadOnlyList<string?>> GetHandleFromUserIdsAsync(IReadOnlyList<string> userIds, OperationContext context)
    {
        List<string> missing = null;
        List<int> missinIndexes = null;
        List<string> result = new List<string>(userIds.Count);
        for (var i = 0; i < userIds.Count; i++)
        {
            if (!_handleCache.TryGetValue(userIds[i], out var userId))
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
                    _handleCache.TryAdd(userIds[index], missingResult);
            }
        }
        return result;
    }
}