using SocialApp.WebApi.Data.Shared;

namespace SocialApp.WebApi.Data.User;

public record ProfileDocument(string UserId, string DisplayName, string Email, string Handle) 
    : Document(Key(UserId))
{
    public static DocumentKey Key(string userId)
    {
        var pk = "user:"+userId;
        return new DocumentKey(pk, "profile");
    }
}