using SocialApp.WebApi.Data._Shared;

namespace SocialApp.WebApi.Features._Shared.Services;

public interface IQueries
{
    IAsyncEnumerable<TResult> QueryManyAsync<TResult>(CosmoContainer container, IQueryMany<TResult> queryMany, OperationContext context);
    Task<TResult> QuerySingleAsync<TResult>(CosmoContainer container, IQuerySingle<TResult> queryMany, OperationContext context);
}