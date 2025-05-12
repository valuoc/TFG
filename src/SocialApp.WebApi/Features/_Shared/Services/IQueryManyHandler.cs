using SocialApp.WebApi.Data._Shared;

namespace SocialApp.WebApi.Features._Shared.Services;

public interface IQueryManyHandler<in TQuery, out TResult>
    where TQuery : IQueryMany<TResult>
{
    IAsyncEnumerable<TResult> ExecuteQueryAsync(CosmoContainer container, TQuery query, OperationContext context);
}

public interface IQuerySingleHandler<in TQuery, TResult>
    where TQuery : IQuerySingle<TResult>
{
    Task<TResult> ExecuteQueryAsync(CosmoContainer container, TQuery query, OperationContext context);
}

public interface IQueryMany<TResult>
{
    
}

public interface IQuerySingle<TResult>
{
    
}


public interface IQueries
{
    IAsyncEnumerable<TResult> ExecuteQueryManyAsync<TResult>(CosmoContainer container, IQueryMany<TResult> queryMany, OperationContext context);
    Task<TResult> ExecuteQuerySingleAsync<TResult>(CosmoContainer container, IQuerySingle<TResult> queryMany, OperationContext context);
}