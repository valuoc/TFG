using SocialApp.WebApi.Data._Shared;

namespace SocialApp.WebApi.Features._Shared.Services;

public interface IQueryManyHandler<in TQuery, out TResult>
    where TQuery : IQueryMany<TResult>
{
    IAsyncEnumerable<TResult> ExecuteQueryAsync(CosmoContainer container, TQuery query, OperationContext context);
}

public interface IQueryMany<TResult>
{
    
}