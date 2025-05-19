using System.Collections.Concurrent;
using System.Reflection;
using SocialApp.WebApi.Data._Shared;

namespace SocialApp.WebApi.Features._Shared.Services;

public sealed class QueryResolver : IQueries
{
    private readonly IServiceProvider _services;
    private readonly ConcurrentDictionary<Type, object> _manyInvocators = new();
    private readonly ConcurrentDictionary<Type, object> _singleInvocators = new();

    public QueryResolver(IServiceProvider services)
        => _services = services;

    public IAsyncEnumerable<TResult> QueryManyAsync<TResult>(CosmoContainer container, IQueryMany<TResult> queryMany, OperationContext context)
    {
        var queryType = queryMany.GetType();

        var wrapper = (QueryManyInvoker<TResult>)_manyInvocators.GetOrAdd(queryType, static (q,s) =>
        {
            var queryManyType = typeof(IQueryManyHandler<,>).MakeGenericType(q, typeof(TResult));
            var handler = s.GetRequiredService(queryManyType);
            var wrapperType = typeof(QueryManyInvoker<,>).MakeGenericType(q, typeof(TResult));
            return Activator.CreateInstance(wrapperType, BindingFlags.CreateInstance, null, [handler], null);
        }, _services);
        
        return wrapper.ExecuteQueryAsync(container, queryMany, context);
    }

    public Task<TResult> QuerySingleAsync<TResult>(CosmoContainer container, IQuerySingle<TResult> queryMany, OperationContext context)
    {
        var queryType = queryMany.GetType();

        var wrapper = (QuerySingleInvoker<TResult>)_singleInvocators.GetOrAdd(queryType, static (q,s) =>
        {
            var querySingleType = typeof(IQuerySingleHandler<,>).MakeGenericType(q, typeof(TResult));
            var handler = s.GetRequiredService(querySingleType);
            var wrapperType = typeof(QuerySingleInvoker<,>).MakeGenericType(q, typeof(TResult));
            return Activator.CreateInstance(wrapperType, BindingFlags.CreateInstance, null, [handler], null);
        }, _services);
        
        return wrapper.ExecuteQueryAsync(container, queryMany, context);
    }
    
    public abstract class QueryManyInvoker<TResult>
    {
        public abstract IAsyncEnumerable<TResult> ExecuteQueryAsync(CosmoContainer container, object query, OperationContext context);
    }
    
    public class QueryManyInvoker<TQuery, TResult> : QueryManyInvoker<TResult>
        where TQuery : IQueryMany<TResult>
    {
        private readonly IQueryManyHandler<TQuery, TResult> _query;
        public QueryManyInvoker(IQueryManyHandler<TQuery, TResult> query)
            => _query = query;
        public override IAsyncEnumerable<TResult> ExecuteQueryAsync(CosmoContainer container, object query, OperationContext context)
            => _query.ExecuteQueryAsync(container, (TQuery)query, context);
    }
    
    public abstract class QuerySingleInvoker<TResult>
    {
        public abstract Task<TResult> ExecuteQueryAsync(CosmoContainer container, object query, OperationContext context);
    }
    
    public class QuerySingleInvoker<TQuery, TResult> : QuerySingleInvoker<TResult>
        where TQuery : IQuerySingle<TResult>
    {
        private readonly IQuerySingleHandler<TQuery, TResult> _query;
        public QuerySingleInvoker(IQuerySingleHandler<TQuery, TResult> query)
            => _query = query;
        public override Task<TResult> ExecuteQueryAsync(CosmoContainer container, object query, OperationContext context)
            => _query.ExecuteQueryAsync(container, (TQuery)query, context);
    }
}