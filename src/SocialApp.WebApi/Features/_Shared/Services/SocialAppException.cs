namespace SocialApp.WebApi.Features._Shared.Services;

public abstract class SocialAppException : Exception
{
    protected SocialAppException(string? message) : base(message)
    {
    }

    protected SocialAppException(string? message, Exception? innerException) : base(message, innerException)
    {
    }
}