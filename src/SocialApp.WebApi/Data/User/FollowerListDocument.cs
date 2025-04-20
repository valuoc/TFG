using SocialApp.WebApi.Data._Shared;

namespace SocialApp.WebApi.Data.User;

public record FollowerListDocument(string UserId)
    : Document(Key(UserId))
{
    public HashSet<string>? Followers { get; set; }
    
    public static DocumentKey Key(string userId)
    {
        return new DocumentKey($"user:{userId}", "follower_list");
    }
}