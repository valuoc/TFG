using SocialApp.WebApi.Data._Shared;

namespace SocialApp.WebApi.Features._Shared.Services;

public interface IQueryMany<in TQuery, out TResult>
{
    IAsyncEnumerable<TResult> ExecuteQueryAsync(CosmoContainer container, TQuery query, OperationContext context);
}

public interface IQuerySingle<in TQuery, TResult>
{
    Task<TResult> ExecuteQueryAsync(CosmoContainer container, TQuery query, OperationContext context);
}


public interface IQueries
{
    IAsyncEnumerable<TResult> ExecuteQueryManyAsync<TQuery, TResult>(CosmoContainer container, TQuery query, OperationContext context);
    Task<TResult> ExecuteQuerySingleAsync<TQuery, TResult>(CosmoContainer container, TQuery query, OperationContext context);

}