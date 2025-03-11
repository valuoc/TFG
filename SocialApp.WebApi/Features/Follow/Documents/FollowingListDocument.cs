using SocialApp.WebApi.Features.Documents;

namespace SocialApp.WebApi.Features.Follow.Documents;

public enum FollowingStatus { PendingAdd, PendingRemove, Ready}
public record FollowingListDocument(string UserId)
    : Document(Key(UserId))
{
    public Dictionary<string, FollowingStatus>? Following { get; set; }
    public static DocumentKey Key(string userId)
    {
        return new DocumentKey($"account:{userId}", "following_list");
    }
}