using SocialApp.WebApi.Data._Shared;

namespace SocialApp.WebApi.Data.Session;

public record PasswordLoginDocument(string UserId, string Email, string Password) 
    : Document(Key(Email))
{
    public static DocumentKey Key(string email)
    {
        var pk = "user:"+email;
        return new DocumentKey(pk, "login_password");
    }
}