using SocialApp.WebApi.Data._Shared;

namespace SocialApp.WebApi.Features._Shared.Services;

public interface IQuerySingle<TResult>
{
    
}

public interface IQuerySingleHandler<in TQuery, TResult>
    where TQuery : IQuerySingle<TResult>
{
    Task<TResult> ExecuteQueryAsync(CosmoContainer container, TQuery query, OperationContext context);
}