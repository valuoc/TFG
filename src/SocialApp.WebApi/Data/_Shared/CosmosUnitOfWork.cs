using System.Linq.Expressions;
using System.Net;
using Microsoft.Azure.Cosmos;
using SocialApp.WebApi.Data.User;
using SocialApp.WebApi.Features._Shared.Services;

namespace SocialApp.WebApi.Data._Shared;

public interface IUnitOfWork
{
    void Create<T>(T document)
        where T:Document;
    void Increment<T>(DocumentKey key, Expression<Func<T,int>> path, int increment = 1)
        where T:Document;
    void CreateOrUpdate<T>(T document)
        where T:Document;
    
    Task SaveChangesAsync(OperationContext context);
}

public class CosmosUnitOfWork : IUnitOfWork
{
    private static readonly TransactionalBatchPatchItemRequestOptions _noBatchResponse = new() {EnableContentResponseOnWrite = false};
    
    private readonly TransactionalBatch _batch;
    private readonly List<UnitOfWorkOperation> _operations;
    public CosmosUnitOfWork(TransactionalBatch batch)
    {
        _batch = batch;
        _operations = new List<UnitOfWorkOperation>();
    }

    public void Create<T>(T document)
        where T:Document
    {
        _operations.Add(new UnitOfWorkOperation(typeof(T), new DocumentKey(document.Pk, document.Id), OperationKind.Create));
        _batch.CreateItem(document, _noBatchResponse);
    }
    
    public void Increment<T>(DocumentKey key,  Expression<Func<T,int>> path, int increment = 1)
        where T:Document
    {
        _operations.Add(new UnitOfWorkOperation(typeof(T), key, OperationKind.Increment));
        var memberName = ((MemberExpression)path.Body).Member.Name;
        memberName = char.ToLowerInvariant(memberName[0]) + memberName[1..];
        _batch.PatchItem(key.Id, [PatchOperation.Increment($"/{memberName}", increment)], _noBatchResponse);
    }

    public void CreateOrUpdate<T>(T document) where T : Document
    {
        _operations.Add(new UnitOfWorkOperation(typeof(T), new DocumentKey(document.Pk, document.Id), OperationKind.CreateOrUpdate));
        _batch.UpsertItem(document, _noBatchResponse);
    }

    public async Task SaveChangesAsync(OperationContext context)
    {
        try
        {
            var response = await _batch.ExecuteAsync(context.Cancellation);
            context.AddRequestCharge(response.RequestCharge);
            ThrowErrorIfTransactionFailed(response);
        }
        catch (CosmosException ex)
        {
            context.AddRequestCharge(ex.RequestCharge);
            throw;
        }
    }

    private void ThrowErrorIfTransactionFailed(TransactionalBatchResponse response)
    {
        if (!response.IsSuccessStatusCode)
        {
            for (var i = 0; i < response.Count; i++)
            {
                var sub = response[i];
                if (sub.StatusCode != HttpStatusCode.FailedDependency)
                {
                    var op = _operations[i];
                    var error = response.StatusCode switch
                    {
                        HttpStatusCode.Conflict => OperationError.Conflict,
                        _ => OperationError.Unknown,
                    };
                    throw new UnitOfWorkException(op, error, response.ErrorMessage);
                }
            }
        }
    }
}

public enum OperationKind { Create,
    Increment,
    CreateOrUpdate
}
public enum OperationError { Unknown, Conflict }
public readonly record struct UnitOfWorkOperation(Type DocumentType, DocumentKey Key, OperationKind Kind);
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