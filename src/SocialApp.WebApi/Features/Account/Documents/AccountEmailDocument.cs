using SocialApp.WebApi.Features.Documents;

namespace SocialApp.WebApi.Features.Account.Documents;

public record AccountEmailDocument(string Email, string UserId) 
    : Document(Key(Email))
{
    public static DocumentKey Key(string email)
    {
        var pk = "email:"+email;
        return new DocumentKey(pk, "email");
    }
}