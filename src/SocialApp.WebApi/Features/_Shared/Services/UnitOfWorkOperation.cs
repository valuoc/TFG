using SocialApp.WebApi.Data._Shared;

namespace SocialApp.WebApi.Features._Shared.Services;

public readonly record struct UnitOfWorkOperation(Type DocumentType, DocumentKey Key, OperationKind Kind, OperationFlags flags, TaskCompletionSource<Document>? Completion = null);