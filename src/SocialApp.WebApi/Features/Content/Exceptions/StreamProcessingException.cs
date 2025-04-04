using SocialApp.WebApi.Features._Shared.Services;

namespace SocialApp.WebApi.Features.Content.Exceptions;

public enum StreamProcessingError
{
    UnexpectedError,
    ThreadToParentComment,
    ThreadToFeed,
    ThreadCountToParentComment,
    ThreadCountToFeed,
    VerifyChildThreadCreation,
    VerifyLikePropagation
}

public sealed class StreamProcessingException : SocialAppException
{
    public StreamProcessingError Error { get; }

    public StreamProcessingException(StreamProcessingError error, string pk, string id) 
        : this(error, pk, id, null) { }

    public StreamProcessingException(StreamProcessingError error, string pk, string id, Exception? innerException) 
        : base($"{error} '{pk}/{id}'", innerException)
    {
        Error = error;
    }
}