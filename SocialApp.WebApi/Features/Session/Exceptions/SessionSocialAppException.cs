using SocialApp.WebApi.Features.Services;

namespace SocialApp.WebApi.Features.Session.Exceptions;


public enum SessionError
{
    UnexpectedError,
    InvalidSession
}

public sealed class SessionSocialAppException : SocialAppException
{
    public SessionError Error { get; }

    public SessionSocialAppException(SessionError error) 
        : this(error, null) { }

    public SessionSocialAppException(SessionError error, Exception? innerException) 
        : base(error.ToString(), innerException)
    {
        Error = error;
    }
}