using SocialApp.WebApi.Features.Documents;

namespace SocialApp.WebApi.Features.Follow.Documents;

public record FollowerListDocument(string UserId)
    : Document(Key(UserId))
{
    public HashSet<string>? Followers { get; set; }
    
    public static DocumentKey Key(string userId)
    {
        return new DocumentKey($"user:{userId}", "follower_list");
    }
}