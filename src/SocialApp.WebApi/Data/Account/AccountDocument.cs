using SocialApp.WebApi.Data.Shared;

namespace SocialApp.WebApi.Data.Account;

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