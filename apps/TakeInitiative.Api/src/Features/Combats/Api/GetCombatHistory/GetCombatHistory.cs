using System.Net;
using FastEndpoints;
using Marten;
using TakeInitiative.Utilities.Extensions;

namespace TakeInitiative.Api.Features.Combats;

public class GetCombatHistory(IDocumentStore Store) : Endpoint<GetCombatRequest, GetCombatHistoryResponse>
{
    public override void Configure()
    {
        Get("/api/combat/{Id}/history");
    }

    public override async Task HandleAsync(GetCombatRequest request, CancellationToken ct)
    {
        var userId = this.GetUserIdOrThrowUnauthorized();

        var result = await Store.Try(
            async (session) =>
            {
                var combat = await session
                    .Query<Combat>()
                    .Where(x => x.Id == request.Id)
                    .Select(c => new
                    {
                        c.CampaignId,
                        c.History
                    })
                    .SingleOrDefaultAsync(ct);
                if (combat == null)
                {
                    ThrowError(x => x.Id, "There is no combat with the given id.");
                }

                // Fetch the campaign and check the user is apart of the campaign
                var userIsInCampaign = await session.Query<CampaignMember>()
                    .AnyAsync(x => x.CampaignId == combat.CampaignId && x.UserId == userId);

                if (!userIsInCampaign)
                {
                    ThrowError("You cannot view combats of a campaign you are not apart of.");
                }

                return new GetCombatHistoryResponse
                {
                    History = [.. combat.History],
                };
            });

        if (result.IsFailure)
        {
            ThrowError(result.Error, (int)HttpStatusCode.ServiceUnavailable);
        }

        await SendAsync(result.Value);
    }
}