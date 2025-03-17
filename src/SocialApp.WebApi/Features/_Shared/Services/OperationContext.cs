namespace SocialApp.WebApi.Features._Shared.Services;

public sealed class OperationContext
{
    private string _failOperation;
    private Exception? _failException;
    private bool _shouldFail;
    private DateTimeOffset? _fixedTime;
    
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
    
    public DateTimeOffset UtcNow 
        => _fixedTime ?? DateTimeOffset.UtcNow;

    public static OperationContext None() => new(CancellationToken.None);

    public OperationContext(CancellationToken cancel)
        => _cancellation = cancel;

    public void SuppressCancellation()
        => _cancellation = CancellationToken.None;

    public void Signal(string operation)
        => _shouldFail = _failOperation == operation && _failException != null;

    public void FailOnSignal(string operation, Exception exception)
    {
        _failOperation = operation;
        _failException = exception;
    }
    
    public void SetTime(DateTimeOffset time)
        => _fixedTime = time;
}