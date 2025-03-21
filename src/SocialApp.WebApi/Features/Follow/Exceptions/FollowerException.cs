using SocialApp.WebApi.Features._Shared.Services;

namespace SocialApp.WebApi.Features.Follow.Exceptions;

public enum FollowerError
{
    UnexpectedError,
    ConcurrencyFailure,
}

public sealed class FollowerException : SocialAppException
{
    public FollowerError Error { get; }

    public FollowerException(FollowerError error) 
        : this(error, null) { }

    public FollowerException(FollowerError error, Exception? innerException) 
        : base(error.ToString(), innerException)
    {
        Error = error;
    }
}