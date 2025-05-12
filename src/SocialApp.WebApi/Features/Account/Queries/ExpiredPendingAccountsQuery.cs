using Microsoft.Azure.Cosmos;
using SocialApp.WebApi.Data._Shared;
using SocialApp.WebApi.Data.Account;
using SocialApp.WebApi.Features._Shared.Services;

namespace SocialApp.WebApi.Features.Account.Queries;

public class ExpiredPendingAccountsQueryMany : IQueryMany<PendingAccountDocument>
{
    public TimeSpan Limit { get; set; }
}

public sealed class ExpiredPendingAccountsCosmosDbQueryManyHandler : IQueryManyHandler<ExpiredPendingAccountsQueryMany, PendingAccountDocument>
{
    public async IAsyncEnumerable<PendingAccountDocument> ExecuteQueryAsync(CosmoContainer container, ExpiredPendingAccountsQueryMany queryMany, OperationContext context)
    {
        var expiryLimit = PendingAccountDocument.Key(Ulid.NewUlid(DateTimeOffset.UtcNow.Add(-queryMany.Limit)).ToString());
        var cosmosQuery = new QueryDefinition("select * from c where c.pk = @pk and c.sk < @id")
            .WithParameter("@pk", expiryLimit.Pk)
            .WithParameter("@id", expiryLimit.Id);

        var list = new List<PendingAccountDocument>();
        await foreach (var document in container.ExecuteQueryReaderAsync(cosmosQuery, expiryLimit.Pk, context))
        {
             yield return (PendingAccountDocument)document;
        }
    }
}