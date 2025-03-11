namespace SocialApp.WebApi.Features.Services;

public sealed class OperationContext
{
    private string _failOperation;
    private Exception? _failException;
    private bool _shouldFail;
    
    private CancellationToken _cancellation;
    public CancellationToken Cancellation
    {
        get
        {
            if(_shouldFail)
                throw _failException!;
            return _cancellation; 
        }
    }

    public static OperationContext None() => new(CancellationToken.None);

    public OperationContext(CancellationToken cancel)
    {
        _cancellation = cancel;
    }
    
    public void SuppressCancellation()
        => _cancellation = CancellationToken.None;

    public void Signal(string operation)
        => _shouldFail = _failOperation == operation && _failException != null;

    public void FailOnSignal(string operation, Exception exception)
    {
        _failOperation = operation;
        _failException = exception;
    }
}