using SocialApp.WebApi.Data._Shared;

namespace SocialApp.WebApi.Data.Account;

public record EmailLockDocument(string Email, string UserId) 
    : Document(Key(Email))
{
    public static DocumentKey Key(string email)
    {
        var pk = "email_lock:"+email;
        return new DocumentKey(pk, "email");
    }
}