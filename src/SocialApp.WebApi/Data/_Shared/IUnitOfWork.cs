using System.Linq.Expressions;
using SocialApp.WebApi.Features._Shared.Services;

namespace SocialApp.WebApi.Data._Shared;

public interface IUnitOfWork
{
    void Create<T>(T document)
        where T:Document;
    void Increment<T>(DocumentKey key, Expression<Func<T,int>> path, int increment = 1)
        where T:Document;
    void Set<T>(DocumentKey key, Expression<Func<T,object>> path, object value)
        where T:Document;
    void CreateOrUpdate<T>(T document)
        where T:Document;
    void Update<T>(T document)
        where T:Document;
    void Delete<T>(T document)
        where T:Document;
    void Delete<T>(DocumentKey key)
        where T:Document;
    
    Task<T> CreateAsync<T>(T document)
        where T:Document;
    Task<T> CreateOrUpdateAsync<T>(T document)
        where T:Document;
    Task<T> UpdateAsync<T>(T document)
        where T:Document;
    
    Task SaveChangesAsync(OperationContext context);
}