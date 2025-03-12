using SocialApp.WebApi.Features.Documents;

namespace SocialApp.WebApi.Features.Account.Documents;

public enum AccountStatus
{
    Pending = 0,
    Completed = 1
}

public record AccountDocument(string UserId, string Email, string Handle, AccountStatus Status) 
    : Document(Key(UserId))
{
    public static DocumentKey Key(string userId)
    {
        var pk = "account:"+userId;
        return new DocumentKey(pk, "account");
    }
}