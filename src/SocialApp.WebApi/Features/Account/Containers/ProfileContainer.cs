using System.Net;
using Microsoft.Azure.Cosmos;
using SocialApp.WebApi.Data.Shared;
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
    
    public async Task CreateUserProfileAsync(string userId, ProfileDocument profile, OperationContext context)
    {
        var batch = Container.CreateTransactionalBatch(new PartitionKey(profile.Pk));
        batch.CreateItem(profile, requestOptions: _transactionNoResponse);
        var response = await batch.ExecuteAsync(context.Cancellation);
        context.AddRequestCharge(response.RequestCharge);
        ThrowErrorIfTransactionFailed(AccountError.UnexpectedError, response);
    }
    
    public async Task<ProfileDocument?> GetProfileAsync(string userId, OperationContext context)
    {
        var profileKey = ProfileDocument.Key(userId);
        
        const string sql = "select * from c where c.pk = @pk and c.type = @a";
        var query = new QueryDefinition(sql)
            .WithParameter("@pk", profileKey.Pk)
            .WithParameter("@a", nameof(ProfileDocument));

        ProfileDocument? profile = null;
        await foreach (var document in ExecuteQueryReaderAsync(query, profileKey.Pk, context))
        {
            if(document is ProfileDocument p)
                profile = p;
            else
                throw new InvalidOperationException("Unexpected document type: " + document.GetType().Name);
        }

        return profile;
    }
    
    public async Task DeleteProfileDataAsync(string userId, OperationContext context)
    {
        try
        {
            var profileKey = ProfileDocument.Key(userId);
            var response = await Container.DeleteItemAsync<ProfileDocument>(profileKey.Id, new PartitionKey(profileKey.Pk), _noResponseContent, context.Cancellation);
            context.AddRequestCharge(response.RequestCharge);
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