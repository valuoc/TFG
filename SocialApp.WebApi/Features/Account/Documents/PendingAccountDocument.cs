using SocialApp.WebApi.Features.Documents;

namespace SocialApp.WebApi.Features.Account.Documents;

public record PendingAccountDocument(string PendingId, string Email, string UserId, string Handle, DateTime CreatedAt) 
    : Document(Key(PendingId))
{
    public static DocumentKey Key(string id)
    {
        return new DocumentKey("pending_accounts", $"pending_account:{id}");
    }
}