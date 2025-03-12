using SocialApp.WebApi.Features.Documents;

namespace SocialApp.WebApi.Features.Account.Documents;

public record PasswordLoginDocument(string UserId, string Email, string Password) 
    : Document(Key(Email))
{
    public static DocumentKey Key(string email)
    {
        var pk = "user:"+email;
        return new DocumentKey(pk, "login_password");
    }
}