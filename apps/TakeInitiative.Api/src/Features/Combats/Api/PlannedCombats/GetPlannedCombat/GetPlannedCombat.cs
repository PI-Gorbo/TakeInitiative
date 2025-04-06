using System.Net;

using FastEndpoints;

using Marten;

using TakeInitiative.Utilities.Extensions;

namespace TakeInitiative.Api.Features.Combats;

public class GetPlannedCombat(IDocumentStore Store) : Endpoint<GetPlannedCombatRequest>
{
    public override void Configure()
    {
        Get("/api/combat/planned");
    }

    public override async Task HandleAsync(GetPlannedCombatRequest req, CancellationToken ct)
    {
        var userId = this.GetUserIdOrThrowUnauthorized();
        var result = await Store.Try(async session =>
        {
            var campaignMember = await session.Query<CampaignMember>()
                .GetCampaignMemberForUserAndCampaign(userId, req.CampaignId);
            if (campaignMember == null)
            {
                ThrowError("Cannot add a planned combat to a campaign you are not a member of.",
                    (int)HttpStatusCode.NotFound);
            }

            if (campaignMember.IsDungeonMaster == false)
            {
                ThrowError("Only the dungeon master can delete planned combats.", (int)HttpStatusCode.Unauthorized);
            }

            // Fetch the planned combat
            var combat = await session.LoadAsync<PlannedCombat>(req.CombatId);
            if (combat == null)
            {
                ThrowError("No combat with the given id exists.", (int)HttpStatusCode.NotFound);
            }

            return combat;
        });

        if (result.IsFailure)
        {
            ThrowError(result.Error, (int)HttpStatusCode.ServiceUnavailable);
        }

        await SendAsync(result.Value);
    }
}