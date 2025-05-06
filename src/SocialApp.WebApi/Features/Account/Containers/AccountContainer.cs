using SocialApp.WebApi.Data._Shared;
using SocialApp.WebApi.Data.Account;

namespace SocialApp.WebApi.Features.Account.Containers;

public sealed class AccountContainer : CosmoContainer
{
    public AccountContainer(AccountDatabase database)
    : base(database, "accounts")
    {
    }
}