using SocialApp.WebApi.Data._Shared;

namespace SocialApp.WebApi.Features._Shared.Services;

public interface IQueryMany<in TQuery, out TResult>
    where TQuery : IQuery<TResult>
{
    IAsyncEnumerable<TResult> ExecuteQueryAsync(CosmoContainer container, TQuery query, OperationContext context);
}

public interface IQuerySingle<in TQuery, TResult>
    where TQuery : IQuery<TResult>
{
    Task<TResult> ExecuteQueryAsync(CosmoContainer container, TQuery query, OperationContext context);
}

public interface IQuery<TResult>
{
    
}


public interface IQueries
{
    IAsyncEnumerable<TResult> ExecuteQueryManyAsync<TResult>(CosmoContainer container, IQuery<TResult> query, OperationContext context);
    Task<TResult> ExecuteQuerySingleAsync<TResult>(CosmoContainer container, IQuery<TResult> query, OperationContext context);
}