using System.Net;
using FastEndpoints;
using Marten;
using Microsoft.AspNetCore.Authentication.Cookies;
using TakeInitiative.Utilities.Extensions;

namespace TakeInitiative.Api.Features.Combats;

public class PutPlannedCombatStage(IDocumentStore Store) : Endpoint<PutPlannedCombatStageRequest, PlannedCombat>
{
    public override void Configure()
    {
        Put("/api/campaign/planned-combat/stage");

    }

    public override async Task HandleAsync(PutPlannedCombatStageRequest req, CancellationToken ct)
    {

        var userId = this.GetUserIdOrThrowUnauthorized();

        var result = await Store.Try(async session =>
        {
            var combat = await session.LoadAsync<PlannedCombat>(req.CombatId);
            if (combat == null)
            {
                ThrowError(x => x.CombatId, "Combat id does not correlate to any known combats.", (int)HttpStatusCode.NotFound);
            }

            // Verify that the user is a member of the campaign & a DM.
            var campaign = await session.LoadAsync<Campaign>(combat.CampaignId);
            if (campaign == null)
            {
                ThrowError(x => x.CombatId, "The combat does not belong to a campaign.", (int)HttpStatusCode.NotFound);
            }

            if (!campaign.isDm(userId))
            {
                ThrowError("Planned combats can only be edited by DMs.");
            }

            // Check if the stage exists.
            if (!combat.Stages.Any(x => x.Id == req.StageId))
            {
                ThrowError(x => x.StageId, "The stage does not exist.");
            }

            // Get the stage form the combat
            var stage = combat.Stages.FirstOrDefault(x => x.Id == req.StageId);
            if (stage == null)
            {
                ThrowError(x => x.StageId, "The stage does not exist.");
            }
            stage.Name = req.Name;

            session.Store(combat);
            await session.SaveChangesAsync();

            return combat;
        });

        if (result.IsFailure)
        {
            ThrowError(result.Error, (int)HttpStatusCode.ServiceUnavailable);
        }

        await SendAsync(result.Value);
    }
}
