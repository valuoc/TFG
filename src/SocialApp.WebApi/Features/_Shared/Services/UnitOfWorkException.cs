namespace SocialApp.WebApi.Features._Shared.Services;

public sealed class UnitOfWorkException : Exception
{
    public UnitOfWorkOperation Operation { get; }
    public OperationError Error { get; }

    public UnitOfWorkException(UnitOfWorkOperation operation, OperationError error, string? message) 
        : base(FormatMessage(message, operation, error))
    {
        Operation = operation;
        Error = error;
    }

    private static string FormatMessage(string? message, UnitOfWorkOperation operation, OperationError error)
    {
        return $"[{operation.DocumentType.Name}][{operation.Kind}] Failed due {error}.\n{message}";
    }

    public UnitOfWorkException(UnitOfWorkOperation operation,  OperationError error, string? message, Exception? innerException) 
        : base(FormatMessage(message, operation, error), innerException)
    {
        Operation = operation;
    }
}