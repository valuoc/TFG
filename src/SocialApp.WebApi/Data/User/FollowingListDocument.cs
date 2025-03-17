using SocialApp.WebApi.Data._Shared;

namespace SocialApp.WebApi.Data.User;

public enum FollowingStatus { PendingAdd, PendingRemove, Ready}
public record FollowingListDocument(string UserId)
    : Document(Key(UserId))
{
    public Dictionary<string, FollowingStatus>? Following { get; set; }
    public static DocumentKey Key(string userId)
    {
        return new DocumentKey($"user:{userId}", "following_list");
    }
}