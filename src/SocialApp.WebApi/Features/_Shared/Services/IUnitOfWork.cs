using System.Linq.Expressions;
using SocialApp.WebApi.Data._Shared;

namespace SocialApp.WebApi.Features._Shared.Services;

public interface IUnitOfWork
{
    void Create<T>(T document, OperationFlags flags = default)
        where T:Document;
    void Increment<T>(DocumentKey key, Expression<Func<T,int>> path, int increment = 1, OperationFlags flags = default)
        where T:Document;
    void Set<T>(DocumentKey key, Expression<Func<T,object>> path, object value, OperationFlags flags = default)
        where T:Document;
    void CreateOrUpdate<T>(T document, OperationFlags flags = default)
        where T:Document;
    void Update<T>(T document, OperationFlags flags = default)
        where T:Document;
    void Delete<T>(T document, OperationFlags flags = default)
        where T:Document;
    void Delete<T>(DocumentKey key, OperationFlags flags = default)
        where T:Document;
    
    Task<T> CreateAsync<T>(T document, OperationFlags flags = default)
        where T:Document;
    Task<T> CreateOrUpdateAsync<T>(T document, OperationFlags flags = default)
        where T:Document;
    Task<T> UpdateAsync<T>(T document, OperationFlags flags = default)
        where T:Document;
    
    Task SaveChangesAsync(OperationContext context);
}

public enum OperationKind { Create,
    Increment,
    CreateOrUpdate,
    Set,
    Update,
    Delete,
    DeleteByKey
}
public enum OperationError { Unknown, Conflict, NotFound,
    PreconditionFailed
}

[Flags]
public enum OperationFlags { None }