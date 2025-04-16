using SocialApp.WebApi.Data.Shared;

namespace SocialApp.WebApi.Data.Account;

public record PasswordLoginDocument(string UserId, string Email, string Password) 
    : Document(Key(Email))
{
    public static DocumentKey Key(string email)
    {
        var pk = "user:"+email;
        return new DocumentKey(pk, "login_password");
    }
}