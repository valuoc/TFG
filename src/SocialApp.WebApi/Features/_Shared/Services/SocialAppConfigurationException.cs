namespace SocialApp.WebApi.Features._Shared.Services;

public sealed class SocialAppConfigurationException : Exception
{
    public SocialAppConfigurationException(string? message) : base(message)
    {
    }

    public SocialAppConfigurationException(string? message, Exception? innerException) : base(message, innerException)
    {
    }
}