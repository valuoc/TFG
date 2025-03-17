using SocialApp.WebApi.Data._Shared;

namespace SocialApp.WebApi.Data.Account;

public record AccountEmailDocument(string Email, string UserId) 
    : Document(Key(Email))
{
    public static DocumentKey Key(string email)
    {
        var pk = "email:"+email;
        return new DocumentKey(pk, "email");
    }
}