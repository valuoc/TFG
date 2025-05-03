using Microsoft.Azure.Cosmos;
using SocialApp.WebApi.Data._Shared;
using SocialApp.WebApi.Data.Account;
using SocialApp.WebApi.Features._Shared.Services;

namespace SocialApp.WebApi.Features.Account.Containers;

public sealed class AccountContainer : CosmoContainer
{
    private static readonly ItemRequestOptions _noResponseContent = new(){ EnableContentResponseOnWrite = false};
    private static readonly TransactionalBatchItemRequestOptions _transactionNoResponse = new() { EnableContentResponseOnWrite = false };
    
    public AccountContainer(AccountDatabase database)
    : base(database, "accounts")
    { }
    
    public async IAsyncEnumerable<PendingAccountDocument> GetExpiredPendingAccountsAsync(TimeSpan timeLimit, OperationContext context)
    {
        var expiryLimit = PendingAccountDocument.Key(Ulid.NewUlid(DateTimeOffset.UtcNow.Add(-timeLimit)).ToString());
        var query = new QueryDefinition("select * from c where c.pk = @pk and c.sk < @id")
            .WithParameter("@pk", expiryLimit.Pk)
            .WithParameter("@id", expiryLimit.Id);
        
        using var iterator = Container.GetItemQueryIterator<PendingAccountDocument>(query, null, new QueryRequestOptions()
        {
            PopulateIndexMetrics = true
        });
        
        while (iterator.HasMoreResults)
        {
            var pendings = await iterator.ReadNextAsync(context.Cancellation);
            context.AddRequestCharge(pendings.RequestCharge);
            context.SaveDebugMetrics(pendings.IndexMetrics);
            foreach (var pending in pendings)
            {
                yield return pending;
            }
        }
    }
}