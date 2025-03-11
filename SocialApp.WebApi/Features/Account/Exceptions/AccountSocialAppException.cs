using SocialApp.WebApi.Features.Services;

namespace SocialApp.WebApi.Features.Account.Exceptions;

public enum AccountError
{
    UnexpectedError,
    EmailAlreadyRegistered,
    HandleAlreadyRegistered
}
public sealed class AccountSocialAppException : SocialAppException
{
    public AccountError Error { get; }

    public AccountSocialAppException(AccountError error) 
        : this(error, null) { }

    public AccountSocialAppException(AccountError error, Exception? innerException) 
        : base(error.ToString(), innerException)
    {
        Error = error;
    }
}