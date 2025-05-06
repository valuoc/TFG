using SocialApp.WebApi.Data._Shared;

namespace SocialApp.WebApi.Features._Shared.Services;

public sealed class Queries : IQueries
{
    private readonly IServiceProvider _services;

    public Queries(IServiceProvider services)
        => _services = services;

    public IAsyncEnumerable<TResult> ExecuteQueryAsync<TQuery, TResult>(CosmoContainer container, TQuery query, OperationContext context)
    {
        var querier = _services.GetRequiredService<IQuery<TQuery, TResult>>();
        return querier.ExecuteQueryAsync(container, query, context);
    }
}