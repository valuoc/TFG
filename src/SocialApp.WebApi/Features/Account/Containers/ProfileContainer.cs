using System.Net;
using Microsoft.Azure.Cosmos;
using SocialApp.WebApi.Data._Shared;
using SocialApp.WebApi.Data.User;
using SocialApp.WebApi.Features._Shared.Services;
using SocialApp.WebApi.Features.Account.Exceptions;

namespace SocialApp.WebApi.Features.Account.Containers;

public sealed class ProfileContainer : CosmoContainer
{
    private static readonly ItemRequestOptions _noResponseContent = new(){ EnableContentResponseOnWrite = false};
    private static readonly TransactionalBatchItemRequestOptions _transactionNoResponse = new() { EnableContentResponseOnWrite = false };
    
    public ProfileContainer(UserDatabase database)
        :base(database)
    { }
    
    public async ValueTask CreateUserProfileAsync(string userId, ProfileDocument profile, OperationContext context)
    {
        var batch = Container.CreateTransactionalBatch(new PartitionKey(profile.Pk));
        batch.CreateItem(profile, requestOptions: _transactionNoResponse);
        batch.CreateItem(new PendingOperationsDocument(userId), _transactionNoResponse);
        var response = await batch.ExecuteAsync(context.Cancellation);
        ThrowErrorIfTransactionFailed(AccountError.UnexpectedError, response);
    }
    
    public async ValueTask<(ProfileDocument?, PendingOperationsDocument?)> GetProfileAsync(string userId, OperationContext context)
    {
        var profileKey = ProfileDocument.Key(userId);
        
        const string sql = "select * from c where c.pk = @pk and c.type in (@a, @b)";
        var query = new QueryDefinition(sql)
            .WithParameter("@pk", profileKey.Pk)
            .WithParameter("@a", nameof(ProfileDocument))
            .WithParameter("@b", nameof(PendingOperationsDocument));

        ProfileDocument? profile = null;
        PendingOperationsDocument? pendingOperations = null;
        await foreach (var document in MultiQueryAsync(query, context))
        {
            if(document is ProfileDocument p)
                profile = p;
            else if(document is PendingOperationsDocument pd)
                pendingOperations = pd;
            else
                throw new InvalidOperationException("Unexpected document type: " + document.GetType().Name);
        }

        return (profile, pendingOperations);
    }
    
    public async Task DeleteProfileDataAsync(string userId, OperationContext context)
    {
        try
        {
            var profileKey = ProfileDocument.Key(userId);
            await Container.DeleteItemAsync<ProfileDocument>(profileKey.Id, new PartitionKey(profileKey.Pk), _noResponseContent, context.Cancellation);
        }
        catch (CosmosException e) when (e.StatusCode == HttpStatusCode.NotFound)
        {
        }
            
        try
        {
            var pendingComments = PendingOperationsDocument.Key(userId);
            await Container.DeleteItemAsync<PendingOperationsDocument>(pendingComments.Id, new PartitionKey(pendingComments.Pk), _noResponseContent, context.Cancellation);
        }
        catch (CosmosException e) when (e.StatusCode == HttpStatusCode.NotFound)
        {
        }
    }
    
    private static void ThrowErrorIfTransactionFailed(AccountError error, TransactionalBatchResponse response)
    {
        if (!response.IsSuccessStatusCode)
        {
            for (var i = 0; i < response.Count; i++)
            {
                var sub = response[i];
                if (sub.StatusCode != HttpStatusCode.FailedDependency)
                    throw new AccountException(error, new CosmosException($"{error}. Batch failed at position [{i}]: {sub.StatusCode}. {response.ErrorMessage}", sub.StatusCode, 0, i.ToString(), 0));
            }
        }
    }
}