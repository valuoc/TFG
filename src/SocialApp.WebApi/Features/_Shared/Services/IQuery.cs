using SocialApp.WebApi.Data._Shared;

namespace SocialApp.WebApi.Features._Shared.Services;

public interface IQuery<in TQuery, out TResult>
{
    IAsyncEnumerable<TResult> ExecuteQueryAsync(CosmoContainer container, TQuery query, OperationContext context);
}

public interface IQueries
{
    IAsyncEnumerable<TResult> ExecuteQueryAsync<TQuery, TResult>(CosmoContainer container, TQuery query, OperationContext context);
}