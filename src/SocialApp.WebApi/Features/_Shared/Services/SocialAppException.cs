namespace SocialApp.WebApi.Features.Services;

public abstract class SocialAppException : Exception
{
    protected SocialAppException(string? message) : base(message)
    {
    }

    protected SocialAppException(string? message, Exception? innerException) : base(message, innerException)
    {
    }
}