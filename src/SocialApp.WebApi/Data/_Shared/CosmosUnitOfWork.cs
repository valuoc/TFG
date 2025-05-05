using System.Linq.Expressions;
using System.Net;
using Microsoft.Azure.Cosmos;
using SocialApp.WebApi.Features._Shared.Services;

namespace SocialApp.WebApi.Data._Shared;

public class CosmosUnitOfWork : IUnitOfWork
{
    private static readonly TransactionalBatchPatchItemRequestOptions _responseOptions = new() {EnableContentResponseOnWrite = true};
    private static readonly TransactionalBatchPatchItemRequestOptions _noResponseOptions = new() {EnableContentResponseOnWrite = true};
    
    private readonly TransactionalBatch _batch;
    private readonly List<UnitOfWorkOperation> _operations;
    public CosmosUnitOfWork(TransactionalBatch batch)
    {
        _batch = batch;
        _operations = new List<UnitOfWorkOperation>();
    }

    public Task<T> CreateAsync<T>(T document)
        where T:Document
    {
        var tcs = new TaskCompletionSource<Document>();
        var key = new DocumentKey(document.Pk, document.Id);
        _operations.Add(new UnitOfWorkOperation(typeof(T), key, OperationKind.Create, tcs));
        _batch.CreateItem(document, _responseOptions);
        return tcs.Task.ContinueWith(t => (T)t.Result);
    }
    
    public void Create<T>(T document)
        where T:Document
    {
        var key = new DocumentKey(document.Pk, document.Id);
        _operations.Add(new UnitOfWorkOperation(typeof(T), key, OperationKind.Create));
        _batch.CreateItem(document, _noResponseOptions);
    }
    
    public void Increment<T>(DocumentKey key, Expression<Func<T,int>> path, int increment = 1)
        where T:Document
    {
        _operations.Add(new UnitOfWorkOperation(typeof(T), key, OperationKind.Increment));
        var memberName = ((MemberExpression)path.Body).Member.Name;
        memberName = char.ToLowerInvariant(memberName[0]) + memberName[1..];
        _batch.PatchItem(key.Id, [PatchOperation.Increment($"/{memberName}", increment)], _noResponseOptions);
    }

    public void Set<T>(DocumentKey key, Expression<Func<T, object>> path, object value) 
        where T : Document
    {
        _operations.Add(new UnitOfWorkOperation(typeof(T), key, OperationKind.Set));
        var memberExpr = path.Body as MemberExpression;
        if (memberExpr == null && path.Body is UnaryExpression { Operand: MemberExpression innerMember })
            memberExpr = innerMember;
        var memberName = char.ToLowerInvariant(memberExpr.Member.Name[0]) + memberExpr.Member.Name[1..];
        _batch.PatchItem(key.Id, [PatchOperation.Set($"/{memberName}", value)], _noResponseOptions);
    }

    public Task<T> CreateOrUpdateAsync<T>(T document) where T : Document
    {
        var tcs = new TaskCompletionSource<Document>();
        var key = new DocumentKey(document.Pk, document.Id);
        _operations.Add(new UnitOfWorkOperation(typeof(T), key, OperationKind.CreateOrUpdate, tcs));
        _batch.UpsertItem(document, new TransactionalBatchItemRequestOptions()
        {
            IfMatchEtag = document.ETag,
            EnableContentResponseOnWrite = true
        });
        return tcs.Task.ContinueWith(t => (T)t.Result);
    }
    
    public void CreateOrUpdate<T>(T document) where T : Document
    {
        var key = new DocumentKey(document.Pk, document.Id);
        _operations.Add(new UnitOfWorkOperation(typeof(T), key, OperationKind.CreateOrUpdate));
        _batch.UpsertItem(document, new TransactionalBatchItemRequestOptions()
        {
            IfMatchEtag = document.ETag,
            EnableContentResponseOnWrite = false
        });
    }
    
    public Task<T> UpdateAsync<T>(T document) where T : Document
    {
        var tcs = new TaskCompletionSource<Document>();
        var key = new DocumentKey(document.Pk, document.Id);
        _operations.Add(new UnitOfWorkOperation(typeof(T), key, OperationKind.Update, tcs));
        _batch.ReplaceItem(document.Id, document, new TransactionalBatchItemRequestOptions
        {
            IfMatchEtag = document.ETag,
            EnableContentResponseOnWrite = true
        });
        return tcs.Task.ContinueWith(t => (T)t.Result);
    }
    
    public void Update<T>(T document) where T : Document
    {
        var key = new DocumentKey(document.Pk, document.Id);
        _operations.Add(new UnitOfWorkOperation(typeof(T), key, OperationKind.Update));
        _batch.ReplaceItem(document.Id, document, new TransactionalBatchItemRequestOptions
        {
            IfMatchEtag = document.ETag,
            EnableContentResponseOnWrite = false
        });
    }

    public void Delete<T>(T document) where T : Document
    {
        _operations.Add(new UnitOfWorkOperation(typeof(T), new DocumentKey(document.Pk, document.Id), OperationKind.Delete));
        _batch.DeleteItem(document.Id, _noResponseOptions);
    }

    public void Delete<T>(DocumentKey key) where T : Document
    {
        _operations.Add(new UnitOfWorkOperation(typeof(T), new DocumentKey(key.Pk, key.Id), OperationKind.DeleteByKey));
        _batch.DeleteItem(key.Id, _noResponseOptions);
    }

    public async Task SaveChangesAsync(OperationContext context)
    {
        try
        {
            if(_operations.Count == 0)
                return;
            
            var response = await _batch.ExecuteAsync(context.Cancellation);
            context.AddRequestCharge(response.RequestCharge);
            ProcessTransactionResponse(response);
            _operations.Clear();
        }
        catch (CosmosException ex)
        {
            context.AddRequestCharge(ex.RequestCharge);
            throw;
        }
    }

    private void ProcessTransactionResponse(TransactionalBatchResponse response)
    {
        if (!response.IsSuccessStatusCode)
        {
            ThrowUnitOfWorkException(response);
        }
        else
        {
            for (var i = 0; i < response.Count; i++)
            {
                var result = response[i];
                var op = _operations[i];

                if (op.Completion != null)
                {
                    var document = DocumentSerialization.DeserializeDocument(result.ResourceStream);
                    op.Completion.SetResult(document);
                }
            }
        }
    }

    private void ThrowUnitOfWorkException(TransactionalBatchResponse response)
    {
        for (var i = 0; i < response.Count; i++)
        {
            var result = response[i];
            if (result.StatusCode != HttpStatusCode.FailedDependency)
            {
                var op = _operations[i];
                var error = response.StatusCode switch
                {
                    HttpStatusCode.Conflict => OperationError.Conflict,
                    HttpStatusCode.NotFound => OperationError.NotFound,
                    HttpStatusCode.PreconditionFailed => OperationError.PreconditionFailed,
                    _ => OperationError.Unknown,
                };
                throw new UnitOfWorkException(op, error, response.ErrorMessage);
            }
        }
    }
}

public enum OperationKind { Create,
    Increment,
    CreateOrUpdate,
    Set,
    Update,
    Delete,
    DeleteByKey
}
public enum OperationError { Unknown, Conflict, NotFound,
    PreconditionFailed
}
public readonly record struct UnitOfWorkOperation(Type DocumentType, DocumentKey Key, OperationKind Kind, TaskCompletionSource<Document>? Completion = null);
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