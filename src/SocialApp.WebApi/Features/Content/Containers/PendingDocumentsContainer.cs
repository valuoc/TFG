using System.Net;
using Microsoft.Azure.Cosmos;
using SocialApp.WebApi.Data._Shared;
using SocialApp.WebApi.Data.User;
using SocialApp.WebApi.Features._Shared.Services;
using SocialApp.WebApi.Features.Session.Models;

namespace SocialApp.WebApi.Features.Content.Containers;

public sealed class PendingDocumentsContainer : CosmoContainer
{
    public PendingDocumentsContainer(UserDatabase database)
        : base(database)
    { }

    public async Task<PendingOperationsDocument> RegisterPendingOperationAsync(UserSession user, PendingOperation operation, OperationContext context)
    {
        user.HasPendingOperations = true;
        var pendingKey = PendingOperationsDocument.Key(user.UserId);
        var response = await Container.PatchItemAsync<PendingOperationsDocument>
        (
            pendingKey.Id, new PartitionKey(pendingKey.Pk),
            [PatchOperation.Add("/items/-", operation)], // patch is case sensitive
            cancellationToken: context.Cancellation
        );
        return response.Resource;
    }

    public async Task ClearPendingOperationAsync(UserSession user, PendingOperationsDocument pending, PendingOperation operation, OperationContext context)
    {
        try
        {
            var index = pending.Items.Select((c, i) => (c, i)).First(x => x.c.Id == operation.Id).i;
            var response = await Container.PatchItemAsync<PendingOperationsDocument>
            (
                pending.Id, new PartitionKey(pending.Pk),
                [PatchOperation.Remove($"/items/{index}")], // patch is case sensitive
                new PatchItemRequestOptions
                {
                    EnableContentResponseOnWrite = true,
                    IfMatchEtag = pending.ETag
                },
                cancellationToken: context.Cancellation
            );
            user.HasPendingOperations = response.Resource?.Items?.Any() ?? false;
        }
        catch (CosmosException e) when (e.StatusCode == HttpStatusCode.Conflict)
        {
            pending = await GetPendingOperationsAsync(pending.UserId, context);
            await ClearPendingOperationAsync(user, pending, operation, context);
        }
    }
    
    public async Task<PendingOperationsDocument> GetPendingOperationsAsync(string userId, OperationContext context)
    {
        var pendingKey = PendingOperationsDocument.Key(userId);
        var response = await Container.ReadItemAsync<PendingOperationsDocument>
        (
            pendingKey.Id, new PartitionKey(pendingKey.Pk),
            cancellationToken: context.Cancellation
        );
        return response.Resource;
    }
}