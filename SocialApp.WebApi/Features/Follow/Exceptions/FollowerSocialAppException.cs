using SocialApp.WebApi.Features.Services;

namespace SocialApp.WebApi.Features.Follow.Exceptions;

public enum FollowerError
{
    UnexpectedError
}

public sealed class FollowerSocialAppException : SocialAppException
{
    public FollowerError Error { get; }

    public FollowerSocialAppException(FollowerError error) 
        : this(error, null) { }

    public FollowerSocialAppException(FollowerError error, Exception? innerException) 
        : base(error.ToString(), innerException)
    {
        Error = error;
    }
}