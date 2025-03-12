using SocialApp.WebApi.Features.Services;

namespace SocialApp.WebApi.Features.Session.Exceptions;


public enum SessionError
{
    UnexpectedError,
    InvalidSession
}

public sealed class SessionException : SocialAppException
{
    public SessionError Error { get; }

    public SessionException(SessionError error) 
        : this(error, null) { }

    public SessionException(SessionError error, Exception? innerException) 
        : base(error.ToString(), innerException)
    {
        Error = error;
    }
}