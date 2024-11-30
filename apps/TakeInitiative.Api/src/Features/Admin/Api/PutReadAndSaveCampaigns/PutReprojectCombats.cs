using FastEndpoints;
using Marten;
using CSharpFunctionalExtensions;

namespace TakeInitiative.Api.Features.Combats;

public class PutReadAndSaveCampaigns(IDocumentSession session) : EndpointWithoutRequest
{
    public override void Configure()
    {
        Put("/api/admin/readAndSave/campaigns");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var result = await Result.Try(async () =>
        {
            var campaigns = await session.Query<Campaign>().ToListAsync(ct);
            session.StoreObjects(campaigns);
            await session.SaveChangesAsync(ct);
        });
    }
}