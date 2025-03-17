using SocialApp.WebApi.Features._Shared.Services;

namespace SocialApp.WebApi.Features.Account.Exceptions;

public enum AccountError
{
    UnexpectedError,
    EmailAlreadyRegistered,
    HandleAlreadyRegistered
}
public sealed class AccountException : SocialAppException
{
    public AccountError Error { get; }

    public AccountException(AccountError error) 
        : this(error, null) { }

    public AccountException(AccountError error, Exception? innerException) 
        : base(error.ToString(), innerException)
    {
        Error = error;
    }
}