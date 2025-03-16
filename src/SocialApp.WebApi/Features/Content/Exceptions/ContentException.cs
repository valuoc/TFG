using SocialApp.WebApi.Features.Services;

namespace SocialApp.WebApi.Features.Content.Exceptions;

public enum ContentError
{
    UnexpectedError,
    CreatePostFailure,
    CreateCommentPostFailure,
    CreateCommentFailure,
    ContentNotFound
}

public sealed class ContentException : SocialAppException
{
    public ContentError Error { get; }

    public ContentException(ContentError error) 
        : this(error, null) { }

    public ContentException(ContentError error, Exception? innerException) 
        : base(error.ToString(), innerException)
    {
        Error = error;
    }
}